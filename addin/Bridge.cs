// Local HTTP bridge: 127.0.0.1 only, token protected. Jobs run on Revit's API thread via ExternalEvent.
// Developed by Dr Ibrahim M.A. Alaamri. MIT License.
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Autodesk.Revit.UI;

namespace Alaamri.RevitMcp
{
    internal class Job
    {
        public string Code;
        public bool UseTransaction = true;
        public string TransactionName;
        public readonly TaskCompletionSource<string> Done = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        public int State; // 0 queued, 1 running, 2 cancelled
    }

    internal class Bridge
    {
        public const int DefaultPort = 48884;
        HttpListener _listener;
        ExternalEvent _event;
        readonly ConcurrentQueue<Job> _queue = new ConcurrentQueue<Job>();
        string _token;
        public int Port { get; private set; }
        public bool IsRunning => _listener != null && _listener.IsListening;
        public long Served;

        public Bridge()
        {
            // ExternalEvent must be created in a valid API context (OnStartup is one).
            _event = ExternalEvent.Create(new Handler(this));
        }

        public string Start()
        {
            if (IsRunning) return null;
            _token = Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
            Exception last = null;
            for (int p = DefaultPort; p < DefaultPort + 20; p++)
            {
                var l = new HttpListener();
                l.Prefixes.Add($"http://127.0.0.1:{p}/");
                try { l.Start(); _listener = l; Port = p; break; }
                catch (Exception ex) { last = ex; try { l.Close(); } catch { } }
            }
            if (_listener == null) return last?.Message ?? "no free port";

            Directory.CreateDirectory(Settings.Dir);
            File.WriteAllText(Settings.InfoPath, JsonSerializer.Serialize(new
            {
                port = Port,
                token = _token,
                pid = Environment.ProcessId,
                revit = App.UiApp.ControlledApplication.VersionName,
                addin_version = App.Version,
                started = DateTime.Now.ToString("s"),
            }));
            Settings.Log($"bridge started on 127.0.0.1:{Port}");
            var l2 = _listener;
            new Thread(() => Loop(l2)) { IsBackground = true, Name = "AlaamriMcpBridge" }.Start();
            return null;
        }

        public void Stop()
        {
            try { _listener?.Stop(); _listener?.Close(); } catch { }
            _listener = null;
            try { if (File.Exists(Settings.InfoPath)) File.Delete(Settings.InfoPath); } catch { }
            while (_queue.TryDequeue(out var j)) j.Done.TrySetResult(Err("bridge stopped"));
            Settings.Log("bridge stopped");
        }

        void Loop(HttpListener l)
        {
            while (l.IsListening)
            {
                HttpListenerContext ctx;
                try { ctx = l.GetContext(); } catch { break; }
                Task.Run(() => HandleAsync(ctx));
            }
        }

        async Task HandleAsync(HttpListenerContext ctx)
        {
            string body;
            int status = 200;
            try
            {
                var req = ctx.Request;
                // Reject browser-originated requests and anything without the per-session token.
                if (req.Headers["Origin"] != null || req.Headers["X-Alaamri-Token"] != _token)
                {
                    status = 403; body = Err("forbidden");
                }
                else if (req.Url.AbsolutePath == "/ping")
                {
                    body = JsonSerializer.Serialize(new
                    {
                        ok = true, addin_version = App.Version, author = App.Author,
                        revit = App.UiApp.ControlledApplication.VersionName,
                        build = App.UiApp.ControlledApplication.VersionBuild, served = Served,
                    });
                }
                else if (req.Url.AbsolutePath == "/exec" && req.HttpMethod == "POST")
                {
                    string raw;
                    using (var sr = new StreamReader(req.InputStream, Encoding.UTF8)) raw = await sr.ReadToEndAsync();
                    using var docj = JsonDocument.Parse(raw);
                    var root = docj.RootElement;
                    var job = new Job
                    {
                        Code = root.GetProperty("code").GetString(),
                        UseTransaction = !root.TryGetProperty("transaction", out var t) || t.GetBoolean(),
                        TransactionName = root.TryGetProperty("transaction_name", out var tn) ? tn.GetString() : null,
                    };
                    int timeout = root.TryGetProperty("timeout_s", out var ts) ? ts.GetInt32() : 120;
                    _queue.Enqueue(job);
                    _event.Raise();
                    var finished = await Task.WhenAny(job.Done.Task, Task.Delay(TimeSpan.FromSeconds(timeout)));
                    if (finished == job.Done.Task) body = job.Done.Task.Result;
                    else if (Interlocked.CompareExchange(ref job.State, 2, 0) == 0)
                        body = Err("timeout: Revit is busy (a dialog or command may be open). The job was cancelled.");
                    else body = await job.Done.Task; // already running: wait for it
                    Interlocked.Increment(ref Served);
                }
                else { status = 404; body = Err("not found"); }
            }
            catch (Exception ex) { status = 500; body = Err(ex.Message); }

            try
            {
                var bytes = Encoding.UTF8.GetBytes(body);
                ctx.Response.StatusCode = status;
                ctx.Response.ContentType = "application/json; charset=utf-8";
                ctx.Response.ContentLength64 = bytes.Length;
                await ctx.Response.OutputStream.WriteAsync(bytes);
                ctx.Response.Close();
            }
            catch { }
        }

        internal static string Err(string m) => JsonSerializer.Serialize(new { ok = false, error = m });

        class Handler : IExternalEventHandler
        {
            readonly Bridge _b;
            public Handler(Bridge b) { _b = b; }
            public string GetName() => "Alaamri Revit MCP";

            public void Execute(UIApplication uiapp)
            {
                while (_b._queue.TryDequeue(out var job))
                {
                    if (Interlocked.CompareExchange(ref job.State, 1, 0) != 0) continue; // cancelled
                    string result;
                    try { result = Engine.Run(uiapp, job); }
                    catch (Exception ex) { result = Err(ex.ToString()); }
                    job.Done.TrySetResult(result);
                }
            }
        }
    }
}
