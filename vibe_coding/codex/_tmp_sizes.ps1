Add-Type -AssemblyName System.Drawing
$paths = @(
  'D:\study_and_work\unity-MK-test\MageKnight_from_zero\Assets\UI\Images\DeckTheme\Ideal\Candidates\Board\deck_ideal_board_candidate_20260104_f.png',
  'D:\study_and_work\unity-MK-test\MageKnight_from_zero\Assets\UI\Images\DeckTheme\Ideal\Candidates\CardBack\deck_ideal_card_back_candidate_20260104_f.png',
  'D:\study_and_work\unity-MK-test\MageKnight_from_zero\Assets\UI\Images\DeckTheme\Ideal\Candidates\CardFace\deck_ideal_card_face_candidate_20260104_a.png'
)
foreach ($p in $paths) {
  $img = [System.Drawing.Image]::FromFile($p)
  Write-Output ($p + ' | ' + $img.Width + 'x' + $img.Height)
  $img.Dispose()
}
