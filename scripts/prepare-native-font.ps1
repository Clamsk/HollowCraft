param([Parameter(Mandatory=$true)][string]$MinecraftJar,[Parameter(Mandatory=$true)][string]$AssetsRoot)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
Add-Type -AssemblyName System.Drawing
$fontZipTask=[IO.Compression.ZipFile]::OpenRead($MinecraftJar)
$fontGlyphsTask=[Collections.Generic.List[object]]::new()
$fontSpacesTask=@{}
function ReadFontJsonTask([string]$Path){
    $fontEntryTask=$fontZipTask.GetEntry($Path);if(-not $fontEntryTask){throw ('Missing font provider: '+$Path)}
    $fontReaderTask=[IO.StreamReader]::new($fontEntryTask.Open())
    try{return $fontReaderTask.ReadToEnd() | ConvertFrom-Json}finally{$fontReaderTask.Dispose()}
}
function ImportFontProviderTask([string]$Name){
    $fontDataTask=ReadFontJsonTask ('assets/minecraft/font/'+$Name+'.json')
    foreach($fontProviderTask in $fontDataTask.providers){
        if($fontProviderTask.type -eq 'reference'){ImportFontProviderTask ($fontProviderTask.id -replace '^minecraft:','');continue}
        if($fontProviderTask.type -eq 'space'){foreach($fontPropertyTask in $fontProviderTask.advances.PSObject.Properties){$fontSpacesTask[$fontPropertyTask.Name]=$fontPropertyTask.Value};continue}
        if($fontProviderTask.type -ne 'bitmap'){continue}
        $fontFileTask=$fontProviderTask.file -replace '^minecraft:',''
        $fontTextureTask='gui_font_'+([IO.Path]::GetFileNameWithoutExtension($fontFileTask))
        $fontTargetTask=Join-Path $AssetsRoot ($fontTextureTask+'.png')
        [IO.Compression.ZipFileExtensions]::ExtractToFile($fontZipTask.GetEntry('assets/minecraft/textures/'+$fontFileTask),$fontTargetTask,$true)
        $fontBitmapTask=[Drawing.Bitmap]::new($fontTargetTask)
        try{
            $fontHeightTask=if($fontProviderTask.height){[int]$fontProviderTask.height}else{8}
            $fontCellHeightTask=$fontBitmapTask.Height/$fontProviderTask.chars.Count
            $fontRowTask=0
            foreach($fontLineTask in $fontProviderTask.chars){
                $fontCodesTask=[Collections.Generic.List[int]]::new()
                for($fontIndexTask=0;$fontIndexTask -lt $fontLineTask.Length;$fontIndexTask++){
                    $fontCodesTask.Add([char]::ConvertToUtf32($fontLineTask,$fontIndexTask));if([char]::IsHighSurrogate($fontLineTask[$fontIndexTask])){$fontIndexTask++}
                }
                $fontCellWidthTask=$fontBitmapTask.Width/$fontCodesTask.Count
                for($fontColTask=0;$fontColTask -lt $fontCodesTask.Count;$fontColTask++){
                    $fontCodeTask=$fontCodesTask[$fontColTask];if($fontCodeTask -eq 0){continue}
                    $fontActualTask=0
                    for($fontPixelXTask=$fontCellWidthTask-1;$fontPixelXTask -ge 0;$fontPixelXTask--){
                        for($fontPixelYTask=0;$fontPixelYTask -lt $fontCellHeightTask;$fontPixelYTask++){
                            if($fontBitmapTask.GetPixel($fontColTask*$fontCellWidthTask+$fontPixelXTask,$fontRowTask*$fontCellHeightTask+$fontPixelYTask).A -gt 0){$fontActualTask=$fontPixelXTask+1;break}
                        }
                        if($fontActualTask -gt 0){break}
                    }
                    $fontGlyphsTask.Add(@{code=$fontCodeTask;texture=$fontTextureTask;x=$fontColTask*$fontCellWidthTask;y=$fontRowTask*$fontCellHeightTask;width=$fontCellWidthTask;height=$fontCellHeightTask;renderHeight=$fontHeightTask;ascent=[int]$fontProviderTask.ascent;advance=[int][Math]::Floor(.5+$fontActualTask*$fontHeightTask/$fontCellHeightTask)+1})
                }
                $fontRowTask++
            }
        }finally{$fontBitmapTask.Dispose()}
    }
}
try{ImportFontProviderTask 'default'}finally{$fontZipTask.Dispose()}
@{schema=1;spaces=$fontSpacesTask;glyphs=$fontGlyphsTask.ToArray()} | ConvertTo-Json -Depth 6 -Compress | Set-Content -LiteralPath (Join-Path $AssetsRoot 'native-font-v5.json') -Encoding UTF8
Write-Output ('Native bitmap font: '+$fontGlyphsTask.Count+' glyphs; original space advances retained.')
