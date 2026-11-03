param([string]$ImagePath,[string]$ResultPath,[string]$Language='auto')
$ErrorActionPreference='Stop'
$utf8=New-Object System.Text.UTF8Encoding($false)
try {
 Add-Type -AssemblyName System.Runtime.WindowsRuntime
 $null=[Windows.Media.Ocr.OcrEngine,Windows.Foundation,ContentType=WindowsRuntime]
 $null=[Windows.Storage.StorageFile,Windows.Storage,ContentType=WindowsRuntime]
 $null=[Windows.Storage.Streams.IRandomAccessStream,Windows.Storage.Streams,ContentType=WindowsRuntime]
 $null=[Windows.Graphics.Imaging.BitmapDecoder,Windows.Graphics.Imaging,ContentType=WindowsRuntime]
 $null=[Windows.Graphics.Imaging.SoftwareBitmap,Windows.Graphics.Imaging,ContentType=WindowsRuntime]
 $null=[Windows.Media.Ocr.OcrResult,Windows.Foundation,ContentType=WindowsRuntime]
 $asTask=([System.WindowsRuntimeSystemExtensions].GetMethods() | Where-Object { $_.Name -eq 'AsTask' -and $_.IsGenericMethod -and $_.GetParameters().Count -eq 1 -and $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncOperation`1' } | Select-Object -First 1)
 function AwaitResult($operation,[Type]$resultType) { $task=$asTask.MakeGenericMethod($resultType).Invoke($null,@($operation)); if(-not $task.Wait(25000)){throw 'OCR timeout.'}; return $task.Result }
 $languages=@([Windows.Media.Ocr.OcrEngine]::AvailableRecognizerLanguages)
 if($Language -eq 'list') { $answer=@{Text='';Language='';Languages=@($languages | ForEach-Object {$_.LanguageTag});Error=''} }
 else {
  if($Language -eq 'auto') { $selected=$languages | Where-Object {$_.LanguageTag -like 'zh-Hans*' -or $_.LanguageTag -eq 'zh-CN'} | Select-Object -First 1; if(-not $selected){$selected=$languages | Where-Object {$_.LanguageTag -like 'en*'} | Select-Object -First 1};if(-not $selected){$selected=$languages | Select-Object -First 1} }
  else { $selected=$languages | Where-Object {$_.LanguageTag -like ($Language+'*')} | Select-Object -First 1 }
  if(-not $selected){throw ('Language not installed: '+$Language+'. Install the corresponding OCR language in Windows Settings > Time & language > Language & region. Available: '+(($languages | ForEach-Object {$_.LanguageTag}) -join ', '))}
  $engine=[Windows.Media.Ocr.OcrEngine]::TryCreateFromLanguage($selected)
  if(-not $engine){throw 'Cannot create the Windows OCR engine.'}
  $file=AwaitResult ([Windows.Storage.StorageFile]::GetFileFromPathAsync($ImagePath)) ([Windows.Storage.StorageFile])
  $stream=AwaitResult ($file.OpenAsync([Windows.Storage.FileAccessMode]::Read)) ([Windows.Storage.Streams.IRandomAccessStream])
  try {
   $decoder=AwaitResult ([Windows.Graphics.Imaging.BitmapDecoder]::CreateAsync($stream)) ([Windows.Graphics.Imaging.BitmapDecoder])
   if($decoder.PixelWidth -gt [Windows.Media.Ocr.OcrEngine]::MaxImageDimension -or $decoder.PixelHeight -gt [Windows.Media.Ocr.OcrEngine]::MaxImageDimension){throw 'Image exceeds the Windows OCR size limit. Capture a smaller region.'}
   $bitmap=AwaitResult ($decoder.GetSoftwareBitmapAsync([Windows.Graphics.Imaging.BitmapPixelFormat]::Bgra8,[Windows.Graphics.Imaging.BitmapAlphaMode]::Premultiplied)) ([Windows.Graphics.Imaging.SoftwareBitmap])
   try { $result=AwaitResult ($engine.RecognizeAsync($bitmap)) ([Windows.Media.Ocr.OcrResult]);$answer=@{Text=(($result.Lines | ForEach-Object {$_.Text}) -join "`r`n");Language=$selected.LanguageTag;Languages=@($languages | ForEach-Object {$_.LanguageTag});Error=''} } finally {$bitmap.Dispose()}
  } finally {$stream.Dispose()}
 }
} catch {$answer=@{Text='';Language='';Languages=@();Error=$_.Exception.Message}}
[IO.File]::WriteAllText($ResultPath,($answer | ConvertTo-Json -Depth 4 -Compress),$utf8)
