@echo off
REM Alaamri Revit MCP - one-click installer (Developed by Dr Ibrahim M.A. Alaamri, MIT)
REM - Inside the repo folder : installs from this folder.
REM - On its own             : downloads the latest version from GitHub.
REM Installs the Revit 2027 add-in + the MCP server (%LOCALAPPDATA%\Alaamri-Revit-MCP) and registers it in Claude Desktop (.bak kept).
set "AMCP_HERE=%~dp0"
powershell -NoProfile -ExecutionPolicy Bypass -Command "iex ((Get-Content -LiteralPath '%~f0' -Raw) -split '#PS-START#')[2]"
echo.
pause
exit /b
#PS-START#
$ErrorActionPreference = 'Stop'
$repoZip = 'https://github.com/engibrrahem-wq/alaamri-revit-mcp/archive/refs/heads/main.zip'
$here    = $env:AMCP_HERE.TrimEnd('\')
$home2   = Join-Path $env:LOCALAPPDATA 'Alaamri-Revit-MCP'
function Say($m, $c = 'Cyan') { Write-Host $m -ForegroundColor $c }
try {
  Say 'Alaamri Revit MCP - installer' 'White'
  # 0. source folder (local repo or GitHub download)
  $src = $here
  if (-not (Test-Path (Join-Path $src 'server.py'))) {
    Say 'Downloading the latest version from GitHub ...'
    $tmp = Join-Path $env:TEMP ('alaamri-revit-mcp-' + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Force $tmp | Out-Null
    Invoke-WebRequest $repoZip -OutFile (Join-Path $tmp 'repo.zip') -UseBasicParsing
    Expand-Archive (Join-Path $tmp 'repo.zip') $tmp -Force
    $src = (Get-ChildItem $tmp -Directory | Select-Object -First 1).FullName
  }
  if (Get-Process Revit -ErrorAction SilentlyContinue) { throw 'Please close Revit first (save your work), then run this again.' }
  # 1. Revit add-in
  $years = @('2027') | Where-Object { Test-Path "C:\Program Files\Autodesk\Revit $_\Revit.exe" }
  if (-not $years) { Say 'WARNING: Revit 2027 was not found. The add-in is installed anyway for Revit 2027.' 'Yellow'; $years = @('2027') }
  foreach ($y in $years) {
    $ad = Join-Path $env:APPDATA "Autodesk\Revit\Addins\$y"
    New-Item -ItemType Directory -Force (Join-Path $ad 'AlaamriRevitMcp') | Out-Null
    Copy-Item (Join-Path $src 'dist\AlaamriRevitMcp\*') (Join-Path $ad 'AlaamriRevitMcp') -Force
    Copy-Item (Join-Path $src 'addin\AlaamriRevitMcp.addin') $ad -Force
    Say "[1/4] Revit $y add-in installed in $ad"
  }
  # 2. Python 3.10+
  $py = $null
  foreach ($cand in @(@('py', '-3'), @('python'))) {
    try { $v = & $cand[0] $cand[1..9] -c "import sys;print('%d.%d'%sys.version_info[:2])" 2>$null; if ($v -and [version]$v -ge [version]'3.10') { $py = $cand; break } } catch {}
  }
  if (-not $py) { throw 'Python 3.10+ not found. Install it from https://www.python.org/downloads/ (tick "Add python.exe to PATH"), then run this again.' }
  New-Item -ItemType Directory -Force $home2 | Out-Null
  $venvPy = Join-Path $home2 '.venv\Scripts\python.exe'
  if (-not (Test-Path $venvPy)) { & $py[0] $py[1..9] -m venv (Join-Path $home2 '.venv'); if ($LASTEXITCODE) { throw 'Could not create the virtual environment.' } }
  Say "[2/4] Python $v, environment $home2"
  # 3. MCP server
  & $venvPy -m pip install --disable-pip-version-check -q --upgrade --force-reinstall $src
  if ($LASTEXITCODE) { throw 'pip install failed (check your internet connection).' }
  $exe = Join-Path $home2 '.venv\Scripts\alaamri-revit-mcp.exe'
  if (-not (Test-Path $exe)) { throw "Install finished but $exe is missing." }
  Say "[3/4] MCP server installed: $exe"
  # 4. Claude Desktop (normal + Microsoft Store install), keeping a backup
  $cfgs = @(Join-Path $env:APPDATA 'Claude\claude_desktop_config.json')
  Get-ChildItem (Join-Path $env:LOCALAPPDATA 'Packages') -Directory -Filter 'Claude_*' -ErrorAction SilentlyContinue |
    ForEach-Object { $cfgs += Join-Path $_.FullName 'LocalCache\Roaming\Claude\claude_desktop_config.json' }
  foreach ($c in $cfgs) {
    New-Item -ItemType Directory -Force (Split-Path $c) | Out-Null
    $j = [pscustomobject]@{}
    if (Test-Path $c) {
      Copy-Item $c "$c.bak" -Force
      $raw = Get-Content $c -Raw
      if ($raw -and $raw.Trim()) { $j = $raw | ConvertFrom-Json }
    }
    if (-not ($j.PSObject.Properties.Name -contains 'mcpServers')) { $j | Add-Member -NotePropertyName mcpServers -NotePropertyValue ([pscustomobject]@{}) }
    $j.mcpServers | Add-Member -NotePropertyName 'alaamri-revit' -NotePropertyValue ([pscustomobject]@{ command = $exe }) -Force
    [IO.File]::WriteAllText($c, ($j | ConvertTo-Json -Depth 32), (New-Object Text.UTF8Encoding $false))
    Say "      registered in $c"
  }
  Say '[4/4] Done.' 'Green'
  Say 'Claude Code users can also run:' 'Yellow'
  Say "  claude mcp add alaamri-revit -- `"$exe`"" 'White'
  Say 'Next: fully quit and reopen Claude Desktop, open Revit, choose "Always Load" for Alaamri Revit MCP,' 'Yellow'
  Say 'and check the Alaamri tab: the "Claude" button is green when connected.' 'Yellow'
} catch {
  Say ('ERROR: ' + $_.Exception.Message) 'Red'
}
