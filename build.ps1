$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
New-Item -ItemType Directory -Force dist | Out-Null
Add-Type -AssemblyName System.Drawing
$bmp = New-Object System.Drawing.Bitmap 256,256
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = 'AntiAlias'
$g.Clear([System.Drawing.Color]::FromArgb(23,23,25))
$gold = [System.Drawing.Color]::FromArgb(219,181,92)
$pen = New-Object System.Drawing.Pen $gold,14
$g.DrawRectangle($pen,42,42,172,172)
$g.DrawArc($pen,89,77,78,74,180,180)
$brush = New-Object System.Drawing.SolidBrush $gold
$g.FillRectangle($brush,80,115,96,66)
$dark = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(23,23,25))
$g.FillEllipse($dark,119,133,18,18)
$g.FillRectangle($dark,124,143,8,19)
$ms = New-Object System.IO.MemoryStream
$bmp.Save($ms,[System.Drawing.Imaging.ImageFormat]::Png)
$png = $ms.ToArray()
$ico = [System.IO.File]::Create((Join-Path $PSScriptRoot 'dist\Vaultline.ico'))
$writer = New-Object System.IO.BinaryWriter $ico
$writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]1)
$writer.Write([byte]0); $writer.Write([byte]0); $writer.Write([byte]0); $writer.Write([byte]0)
$writer.Write([uint16]1); $writer.Write([uint16]32); $writer.Write([uint32]$png.Length); $writer.Write([uint32]22); $writer.Write($png)
$writer.Dispose(); $ms.Dispose(); $g.Dispose(); $bmp.Dispose(); $pen.Dispose(); $brush.Dispose(); $dark.Dispose()
$compiler = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $compiler /nologo /target:winexe /platform:anycpu /optimize+ /out:dist\Vaultline.exe /win32icon:dist\Vaultline.ico /win32manifest:src\app.manifest /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Web.Extensions.dll src\Core.cs src\App.cs src\Tests.cs src\StoragePaths.cs src\AssemblyInfo.cs
if ($LASTEXITCODE -ne 0) { throw 'Application compilation failed' }
Copy-Item src\Vaultline.exe.config dist\Vaultline.exe.config -Force
& $compiler /nologo /target:winexe /out:dist\Uninstall.exe /win32icon:dist\Vaultline.ico /win32manifest:src\app.manifest /r:System.Windows.Forms.dll src\Uninstall.cs src\AssemblyInfo.cs
if ($LASTEXITCODE -ne 0) { throw 'Uninstaller compilation failed' }
& $compiler /nologo /target:winexe /out:dist\Vaultline-Setup.exe /win32icon:dist\Vaultline.ico /win32manifest:src\app.manifest /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:Microsoft.CSharp.dll /resource:dist\Vaultline.exe,Vaultline.exe /resource:dist\Vaultline.ico,Vaultline.ico /resource:dist\Uninstall.exe,Uninstall.exe /resource:dist\Vaultline.exe.config,Vaultline.exe.config src\Setup.cs src\StoragePaths.cs src\AssemblyInfo.cs
if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed' }
Write-Output 'Build completed: dist\Vaultline-Setup.exe'

New-Item -ItemType Directory -Force release | Out-Null
Copy-Item dist\Vaultline-Setup.exe release\Vaultline-Setup.exe -Force
