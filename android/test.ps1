$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
New-Item -ItemType Directory -Path build\test-classes,build\interop -Force | Out-Null
$compiler = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $compiler /nologo /target:exe /out:build\DesktopInterop.exe /r:System.Web.Extensions.dll ..\src\Core.cs tests\DesktopInterop.cs
if ($LASTEXITCODE -ne 0) { throw 'Compile Windows interop test failed' }
& .\build\DesktopInterop.exe generate build\interop
if ($LASTEXITCODE -ne 0) { throw 'Generate Windows fixtures failed' }
& "$env:JAVA_HOME\bin\javac.exe" --release 8 -encoding UTF-8 -cp tests\json.jar -d build\test-classes src\app\vaultline\VaultCore.java tests\CoreTest.java
if ($LASTEXITCODE -ne 0) { throw 'Compile Android core test failed' }
& "$env:JAVA_HOME\bin\java.exe" -cp 'build\test-classes;tests\json.jar' CoreTest build\interop
if ($LASTEXITCODE -ne 0) { throw 'Android core tests failed' }
& .\build\DesktopInterop.exe verify build\interop
if ($LASTEXITCODE -ne 0) { throw 'Android to Windows interop failed' }
