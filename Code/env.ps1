# env.ps1 - add Mosquitto CLI and Flutter to PATH for THIS PowerShell window only (no permanent change).
# Usage (note the leading dot), from the Week-13 folder:
#   Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass -Force
#   . .\Code\env.ps1
$env:Path = "C:\Program Files\mosquitto;C:\src\flutter\bin;$env:Path"
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
Write-Host "PATH ready: mosquitto_pub, mosquitto_sub, flutter" -ForegroundColor Green
