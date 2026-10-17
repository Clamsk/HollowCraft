param([switch]$ValidateOnly)
$ErrorActionPreference='Stop'
$nativeRootTask=Split-Path $PSScriptRoot -Parent
$catalogTask=Get-Content -LiteralPath (Join-Path $nativeRootTask 'release\bosses.json') -Raw -Encoding UTF8 | ConvertFrom-Json
if($catalogTask.schema -ne 1){throw 'Unsupported boss catalogue.'}
. (Join-Path $PSScriptRoot 'native-launcher-process.ps1')
if($ValidateOnly){Write-Output ('Native launcher: '+$catalogTask.items.Count+' bosses; standalone launch path.');return}
Add-Type -AssemblyName System.Windows.Forms,System.Drawing
[Windows.Forms.Application]::EnableVisualStyles()
$formTask=New-Object Windows.Forms.Form
$formTask.Text='HollowCraft Native / 独立版';$formTask.ClientSize=New-Object Drawing.Size(700,490)
$formTask.StartPosition='CenterScreen';$formTask.Font=New-Object Drawing.Font('Microsoft YaHei UI',10)
$formTask.BackColor=[Drawing.Color]::FromArgb(23,29,39);$formTask.ForeColor=[Drawing.Color]::WhiteSmoke
function NativeLabelTask([string]$Text,[int]$Y,[int]$Height=30) {
    $labelTask=New-Object Windows.Forms.Label;$labelTask.Text=$Text;$labelTask.Location=New-Object Drawing.Point(24,$Y);$labelTask.Size=New-Object Drawing.Size(652,$Height);$formTask.Controls.Add($labelTask);return $labelTask
}
$null=NativeLabelTask 'HollowCraft Native · 0.40.0 alpha 4' 20 35
$null=NativeLabelTask '只启动空洞 / Hollow Knight only · 迁移预览，功能尚未齐全' 58 30
$normalTask=New-Object Windows.Forms.Button;$normalTask.Text='继续冒险 / Play';$normalTask.Location=New-Object Drawing.Point(24,100);$normalTask.Size=New-Object Drawing.Size(315,44)
$installTask=New-Object Windows.Forms.Button;$installTask.Text='安装 / 更新 / Install';$installTask.Location=New-Object Drawing.Point(361,100);$installTask.Size=New-Object Drawing.Size(315,44)
$formTask.Controls.AddRange(@($normalTask,$installTask))
$null=NativeLabelTask 'Boss 录制 / Recording：独立存档、生存模式、初始装备' 164
$bossTask=New-Object Windows.Forms.ComboBox;$bossTask.DropDownStyle='DropDownList';$bossTask.Location=New-Object Drawing.Point(24,202);$bossTask.Size=New-Object Drawing.Size(450,30);$bossTask.DisplayMember='Title'
foreach($itemTask in $catalogTask.items){$null=$bossTask.Items.Add([pscustomobject]@{Title=($itemTask.zh+' / '+$itemTask.en);Id=$itemTask.id;Godhome=$itemTask.godhome})}
if($bossTask.Items.Count){$bossTask.SelectedIndex=0}
$levelTask=New-Object Windows.Forms.ComboBox;$levelTask.DropDownStyle='DropDownList';$levelTask.Location=New-Object Drawing.Point(490,202);$levelTask.Size=New-Object Drawing.Size(186,30);$null=$levelTask.Items.AddRange(@('调谐 / Attuned','进升 / Ascended','辐辉 / Radiant'));$levelTask.SelectedIndex=0
$formTask.Controls.AddRange(@($bossTask,$levelTask))
$recordTask=New-Object Windows.Forms.Button;$recordTask.Text='开始 Boss / Record';$recordTask.Location=New-Object Drawing.Point(24,250);$recordTask.Size=New-Object Drawing.Size(315,44)
$stopTask=New-Object Windows.Forms.Button;$stopTask.Text='关闭独立版 / Close';$stopTask.Location=New-Object Drawing.Point(361,250);$stopTask.Size=New-Object Drawing.Size(315,44)
$formTask.Controls.AddRange(@($recordTask,$stopTask))
$statusTask=NativeLabelTask 'A/D 移动 · 空格跳跃 · E 背包 · F 副手 · F3+F4 模式' 319 34
$detailsTask=New-Object Windows.Forms.TextBox
$detailsTask.Multiline=$true;$detailsTask.ReadOnly=$true;$detailsTask.ScrollBars='Vertical'
$detailsTask.Location=New-Object Drawing.Point(24,360);$detailsTask.Size=New-Object Drawing.Size(652,104)
$detailsTask.BackColor=$formTask.BackColor;$detailsTask.ForeColor=$formTask.ForeColor
$detailsTask.Text='启动结果会显示在这里 / Launch results appear here.';$formTask.Controls.Add($detailsTask)
$script:nativeHelperStateTask=@{Helper=$null}
$actionButtonsTask=@($normalTask,$installTask,$recordTask,$stopTask)
function SetNativeBusyTask([bool]$Busy) {foreach($buttonTask in $actionButtonsTask){$buttonTask.Enabled=-not $Busy}}
function ReportNativeResultTask($Result) {
    $statusTask.Text=if($Result.ExitCode -eq 0){'完成 / Ready'}else{'操作未完成 / See details below'}
    $detailsTask.Text=$Result.Message
}
function RunNativeHelperTask([ValidateSet('Play','Install','Close')][string]$Action,[string]$BossId='',[int]$BossLevel=0) {
    if($null -ne $script:nativeHelperStateTask.Helper){return}
    SetNativeBusyTask $true
    try {
        $logRootTask=Join-Path $nativeRootTask 'runtime';New-Item -ItemType Directory -Path $logRootTask -Force | Out-Null
        $helperPathTask=Join-Path $PSScriptRoot 'native-launcher-run.ps1'
        $argumentsTask='-NoProfile -NonInteractive -ExecutionPolicy Bypass -File "'+$helperPathTask+'" -Action '+$Action
        if($BossId){
            if($BossId -notmatch '^[a-zA-Z0-9_-]+$'){throw 'Invalid Boss id.'}
            $argumentsTask+=' -BossId '+$BossId+' -BossLevel '+$BossLevel+' -Width 1920 -Height 1080'
        }
        $shellTask=Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'
        $processTask=Start-Process -FilePath $shellTask -ArgumentList $argumentsTask -WindowStyle Hidden -RedirectStandardOutput (Join-Path $logRootTask 'native-launcher.stdout.log') -RedirectStandardError (Join-Path $logRootTask 'native-launcher.stderr.log') -PassThru
        $null=$processTask.Handle # Keep the exit status available in Windows PowerShell 5.1.
        $script:nativeHelperStateTask.Helper=$processTask
        $statusTask.Text='正在处理 / Working…';$detailsTask.Text=''
    } catch {
        SetNativeBusyTask $false
        ReportNativeResultTask ([pscustomobject]@{ExitCode=1;Message=$_.Exception.Message})
    }
}
$normalTask.Add_Click({RunNativeHelperTask 'Play'})
$installTask.Add_Click({RunNativeHelperTask 'Install'})
$recordTask.Add_Click({
    $selectionTask=$bossTask.SelectedItem
    if($selectionTask){$chosenLevelTask=if($selectionTask.Godhome){$levelTask.SelectedIndex}else{0};RunNativeHelperTask 'Play' $selectionTask.Id $chosenLevelTask}
})
$stopTask.Add_Click({RunNativeHelperTask 'Close'})
$timerTask=New-Object Windows.Forms.Timer;$timerTask.Interval=500
$timerTask.Add_Tick({
    try {
        $resultTask=Receive-HollowCraftNativeHelperTask $script:nativeHelperStateTask (Join-Path $nativeRootTask 'runtime\native-launcher.stdout.log') (Join-Path $nativeRootTask 'runtime\native-launcher.stderr.log')
        if($null -ne $resultTask){SetNativeBusyTask $false;ReportNativeResultTask $resultTask}
    } catch {
        SetNativeBusyTask $false
        ReportNativeResultTask ([pscustomobject]@{ExitCode=1;Message=$_.Exception.Message})
    }
})
$timerTask.Start()
try {[Windows.Forms.Application]::Run($formTask)} finally {
    $timerTask.Stop();$timerTask.Dispose()
    $processTask=$script:nativeHelperStateTask.Helper;$script:nativeHelperStateTask.Helper=$null
    if($null -ne $processTask){$processTask.Dispose()}
    $formTask.Dispose()
}
