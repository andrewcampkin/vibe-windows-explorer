# PowerShell script to generate Windows Explorer app assets
# This script creates simple placeholder icons for the application

$assetsPath = "WindowsExplorer\Assets"
if (-not (Test-Path $assetsPath)) {
    New-Item -ItemType Directory -Path $assetsPath -Force | Out-Null
}

# Add System.Drawing assembly
Add-Type -AssemblyName System.Drawing

function CreateImage {
    param(
        [int]$width,
        [int]$height,
        [string]$outputPath,
        [string]$backgroundColor = "#0078D4",
        [string]$foregroundColor = "#FFFFFF"
    )
    
    $bitmap = New-Object System.Drawing.Bitmap([int]$width, [int]$height)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.Clear([System.Drawing.Color]::FromArgb(0, 0, 0, 0)) # Transparent background
    
    # Draw background circle/rounded rectangle
    $bgBrush = New-Object System.Drawing.SolidBrush([System.Drawing.ColorTranslator]::FromHtml($backgroundColor))
    $rectX = [float]2
    $rectY = [float]2
    $rectWidth = [float]($width - 4)
    $rectHeight = [float]($height - 4)
    $graphics.FillEllipse($bgBrush, $rectX, $rectY, $rectWidth, $rectHeight)
    
    # Draw folder icon (simple representation)
    $fgBrush = New-Object System.Drawing.SolidBrush([System.Drawing.ColorTranslator]::FromHtml($foregroundColor))
    
    # Draw folder shape
    $folderWidth = [float]($width * 0.7)
    $folderHeight = [float]($height * 0.6)
    $folderX = [float](($width - $folderWidth) / 2)
    $folderY = [float](($height - $folderHeight) / 2 + $height * 0.1)
    
    # Folder tab
    $tabWidth = [float]($folderWidth * 0.4)
    $tabHeight = [float]($folderHeight * 0.2)
    $graphics.FillEllipse($fgBrush, $folderX, $folderY, $tabWidth, $tabHeight)
    
    # Folder body
    $points = @(
        [System.Drawing.PointF]::new($folderX, $folderY + $tabHeight / 2),
        [System.Drawing.PointF]::new($folderX + $tabWidth / 2, $folderY + $tabHeight / 2),
        [System.Drawing.PointF]::new($folderX + $tabWidth / 2, $folderY + $folderHeight),
        [System.Drawing.PointF]::new($folderX + $folderWidth, $folderY + $folderHeight),
        [System.Drawing.PointF]::new($folderX + $folderWidth, $folderY + $tabHeight / 2)
    )
    $graphics.FillPolygon($fgBrush, $points)
    
    $graphics.Dispose()
    $bitmap.Save($outputPath, [System.Drawing.Imaging.ImageFormat]::Png)
    $bitmap.Dispose()
    $bgBrush.Dispose()
    $fgBrush.Dispose()
    Write-Host "Created: $outputPath"
}

# Color scheme - Windows blue
$primaryColor = "#0078D4"
$whiteColor = "#FFFFFF"

# Generate all required assets
Write-Host "Generating Windows Explorer assets..."

# Store Logo (50x50)
CreateImage -width 50 -height 50 -outputPath "$assetsPath\StoreLogo.png" -backgroundColor $primaryColor -foregroundColor $whiteColor

# Square 44x44 Logo (44x44)
CreateImage -width 44 -height 44 -outputPath "$assetsPath\Square44x44Logo.png" -backgroundColor $primaryColor -foregroundColor $whiteColor
CreateImage -width 88 -height 88 -outputPath "$assetsPath\Square44x44Logo.scale-200.png" -backgroundColor $primaryColor -foregroundColor $whiteColor
CreateImage -width 24 -height 24 -outputPath "$assetsPath\Square44x44Logo.targetsize-24_altform-unplated.png" -backgroundColor $primaryColor -foregroundColor $whiteColor

# Square 150x150 Logo (150x150)
CreateImage -width 150 -height 150 -outputPath "$assetsPath\Square150x150Logo.png" -backgroundColor $primaryColor -foregroundColor $whiteColor
CreateImage -width 300 -height 300 -outputPath "$assetsPath\Square150x150Logo.scale-200.png" -backgroundColor $primaryColor -foregroundColor $whiteColor

# Wide 310x150 Logo (310x150)
$bitmap = New-Object System.Drawing.Bitmap(310, 150)
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$graphics.Clear([System.Drawing.Color]::FromArgb(0, 0, 0, 0))
$bgBrush = New-Object System.Drawing.SolidBrush([System.Drawing.ColorTranslator]::FromHtml($primaryColor))
$fgBrush = New-Object System.Drawing.SolidBrush([System.Drawing.ColorTranslator]::FromHtml($whiteColor))
# Draw rounded rectangle using path
$path = New-Object System.Drawing.Drawing2D.GraphicsPath
$rectX = [float]5
$rectY = [float]5
$rectWidth = [float]300
$rectHeight = [float]140
$cornerRadius = [float]10
$path.AddArc($rectX, $rectY, $cornerRadius * 2, $cornerRadius * 2, 180, 90)
$path.AddArc($rectX + $rectWidth - ($cornerRadius * 2), $rectY, $cornerRadius * 2, $cornerRadius * 2, 270, 90)
$path.AddArc($rectX + $rectWidth - ($cornerRadius * 2), $rectY + $rectHeight - ($cornerRadius * 2), $cornerRadius * 2, $cornerRadius * 2, 0, 90)
$path.AddArc($rectX, $rectY + $rectHeight - ($cornerRadius * 2), $cornerRadius * 2, $cornerRadius * 2, 90, 90)
$path.CloseFigure()
$graphics.FillPath($bgBrush, $path)
$path.Dispose()
# Draw folder icon
$folderWidth = [float]80
$folderHeight = [float]60
$folderX = [float]20
$folderY = [float]45
$tabWidth = [float]32
$tabHeight = [float]12
$graphics.FillEllipse($fgBrush, $folderX, $folderY, $tabWidth, $tabHeight)
$points = @(
    [System.Drawing.PointF]::new($folderX, $folderY + $tabHeight / 2),
    [System.Drawing.PointF]::new($folderX + $tabWidth / 2, $folderY + $tabHeight / 2),
    [System.Drawing.PointF]::new($folderX + $tabWidth / 2, $folderY + $folderHeight),
    [System.Drawing.PointF]::new($folderX + $folderWidth, $folderY + $folderHeight),
    [System.Drawing.PointF]::new($folderX + $folderWidth, $folderY + $tabHeight / 2)
)
$graphics.FillPolygon($fgBrush, $points)
$graphics.Dispose()
$bitmap.Save("$assetsPath\Wide310x150Logo.png", [System.Drawing.Imaging.ImageFormat]::Png)
$bitmap.Dispose()
$bgBrush.Dispose()
$fgBrush.Dispose()
Write-Host "Created: $assetsPath\Wide310x150Logo.png"

$bitmap = New-Object System.Drawing.Bitmap(620, 300)
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$graphics.Clear([System.Drawing.Color]::FromArgb(0, 0, 0, 0))
# Create new brushes for this section
$bgBrush2 = New-Object System.Drawing.SolidBrush([System.Drawing.ColorTranslator]::FromHtml($primaryColor))
$fgBrush2 = New-Object System.Drawing.SolidBrush([System.Drawing.ColorTranslator]::FromHtml($whiteColor))
# Draw rounded rectangle using path
$path = New-Object System.Drawing.Drawing2D.GraphicsPath
$rectX = [float]10
$rectY = [float]10
$rectWidth = [float]600
$rectHeight = [float]280
$cornerRadius = [float]20
$path.AddArc($rectX, $rectY, $cornerRadius * 2, $cornerRadius * 2, 180, 90)
$path.AddArc($rectX + $rectWidth - ($cornerRadius * 2), $rectY, $cornerRadius * 2, $cornerRadius * 2, 270, 90)
$path.AddArc($rectX + $rectWidth - ($cornerRadius * 2), $rectY + $rectHeight - ($cornerRadius * 2), $cornerRadius * 2, $cornerRadius * 2, 0, 90)
$path.AddArc($rectX, $rectY + $rectHeight - ($cornerRadius * 2), $cornerRadius * 2, $cornerRadius * 2, 90, 90)
$path.CloseFigure()
$graphics.FillPath($bgBrush2, $path)
$path.Dispose()
# Draw folder icon
$folderWidth = [float]160
$folderHeight = [float]120
$folderX = [float]40
$folderY = [float]90
$tabWidth = [float]64
$tabHeight = [float]24
$graphics.FillEllipse($fgBrush2, $folderX, $folderY, $tabWidth, $tabHeight)
$points = @(
    [System.Drawing.PointF]::new($folderX, $folderY + $tabHeight / 2),
    [System.Drawing.PointF]::new($folderX + $tabWidth / 2, $folderY + $tabHeight / 2),
    [System.Drawing.PointF]::new($folderX + $tabWidth / 2, $folderY + $folderHeight),
    [System.Drawing.PointF]::new($folderX + $folderWidth, $folderY + $folderHeight),
    [System.Drawing.PointF]::new($folderX + $folderWidth, $folderY + $tabHeight / 2)
)
$graphics.FillPolygon($fgBrush2, $points)
$graphics.Dispose()
$bitmap.Save("$assetsPath\Wide310x150Logo.scale-200.png", [System.Drawing.Imaging.ImageFormat]::Png)
$bitmap.Dispose()
$bgBrush2.Dispose()
$fgBrush2.Dispose()
Write-Host "Created: $assetsPath\Wide310x150Logo.scale-200.png"

# Splash Screen (620x300 for scale-200)
$bitmap = New-Object System.Drawing.Bitmap(620, 300)
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$graphics.Clear([System.Drawing.ColorTranslator]::FromHtml($primaryColor))
$fgBrush = New-Object System.Drawing.SolidBrush([System.Drawing.ColorTranslator]::FromHtml($whiteColor))
$folderWidth = 200
$folderHeight = 150
$folderX = 210
$folderY = 75
$tabWidth = 80
$tabHeight = 30
$graphics.FillEllipse($fgBrush, $folderX, $folderY, $tabWidth, $tabHeight)
$points = @(
    [System.Drawing.PointF]::new($folderX, $folderY + $tabHeight / 2),
    [System.Drawing.PointF]::new($folderX + $tabWidth / 2, $folderY + $tabHeight / 2),
    [System.Drawing.PointF]::new($folderX + $tabWidth / 2, $folderY + $folderHeight),
    [System.Drawing.PointF]::new($folderX + $folderWidth, $folderY + $folderHeight),
    [System.Drawing.PointF]::new($folderX + $folderWidth, $folderY + $tabHeight / 2)
)
$graphics.FillPolygon($fgBrush, $points)
$graphics.Dispose()
$bitmap.Save("$assetsPath\SplashScreen.scale-200.png", [System.Drawing.Imaging.ImageFormat]::Png)
$bitmap.Dispose()
$fgBrush.Dispose()
Write-Host "Created: $assetsPath\SplashScreen.scale-200.png"

# Lock Screen Logo (24x24)
CreateImage -width 24 -height 24 -outputPath "$assetsPath\LockScreenLogo.scale-200.png" -backgroundColor $primaryColor -foregroundColor $whiteColor

# Create non-scaled versions for manifest compatibility
# Square 44x44 (base version)
Copy-Item "$assetsPath\Square44x44Logo.scale-200.png" "$assetsPath\Square44x44Logo.png" -ErrorAction SilentlyContinue
# Square 150x150 (base version)  
Copy-Item "$assetsPath\Square150x150Logo.scale-200.png" "$assetsPath\Square150x150Logo.png" -ErrorAction SilentlyContinue
# Wide 310x150 (base version)
Copy-Item "$assetsPath\Wide310x150Logo.scale-200.png" "$assetsPath\Wide310x150Logo.png" -ErrorAction SilentlyContinue
# Splash Screen (base version - 310x150)
$bitmap = New-Object System.Drawing.Bitmap(310, 150)
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$graphics.Clear([System.Drawing.ColorTranslator]::FromHtml($primaryColor))
$fgBrush = New-Object System.Drawing.SolidBrush([System.Drawing.ColorTranslator]::FromHtml($whiteColor))
$folderWidth = 100
$folderHeight = 75
$folderX = 105
$folderY = 37.5
$tabWidth = 40
$tabHeight = 15
$graphics.FillEllipse($fgBrush, $folderX, $folderY, $tabWidth, $tabHeight)
$points = @(
    [System.Drawing.PointF]::new($folderX, $folderY + $tabHeight / 2),
    [System.Drawing.PointF]::new($folderX + $tabWidth / 2, $folderY + $tabHeight / 2),
    [System.Drawing.PointF]::new($folderX + $tabWidth / 2, $folderY + $folderHeight),
    [System.Drawing.PointF]::new($folderX + $folderWidth, $folderY + $folderHeight),
    [System.Drawing.PointF]::new($folderX + $folderWidth, $folderY + $tabHeight / 2)
)
$graphics.FillPolygon($fgBrush, $points)
$graphics.Dispose()
$bitmap.Save("$assetsPath\SplashScreen.png", [System.Drawing.Imaging.ImageFormat]::Png)
$bitmap.Dispose()
$fgBrush.Dispose()
Write-Host "Created: $assetsPath\SplashScreen.png"

Write-Host "`nAll assets generated successfully!"

