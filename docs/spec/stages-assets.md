# ステージとアセットの仕様

### Phase 15 の合意仕様（統合仕上げ・2026-07-16 作者合意）

- 勝敗条件はテスト用ではなく本番システム。今回は**ターン制限のみ追加**
  （StageData.turnLimit。0=無制限。最終ターンの敵フェイズ終了時に勝敗未決なら敗北。
  判定は増加前に行うので表示が制限を超えない。制限中は「ターン n／上限」表示）。
  拠点到達などの勝利条件バリエーションは次期ロードマップへ先送り（作者合意）
- 盤面サイズはステージごとに可変（StageData.gridWidth / gridHeight。0以下なら GridManager の
  Inspector 値）。カメラは GridManager.FitCamera が自動フィット（余白は cameraMargin）
- 新ユニット5種: 味方斧兵・味方斧騎兵・敵魔道士・敵槍飛行兵・敵斧騎兵（能力値は初期案。作者が調整）
- 正式ステージ Stage_01: 14×12・ターン制限20・味方10体対敵12体。南が自軍、中央に山脈と森、
  北に城壁（門は敵弓兵が守る。当時は対空の役。飛行兵は城壁を越えられる）。BattleScene の BattleSetup は Stage_01 を参照中
  （Stage_Test に戻すのも Inspector の差し替えだけ）
アセット実体: Assets/_Game/Data/
（Unit_Player, Unit_Enemy, Unit_Player_Archer, Unit_Player_Mage, Unit_Player_Cavalry,
  Unit_Player_Transport, Unit_Player_Flier, Unit_Player_AxeFighter, Unit_Player_AxeCavalry,
  Unit_Enemy_Lancer, Unit_Enemy_Archer, Unit_Enemy_Flier, Unit_Enemy_Mage, Unit_Enemy_LanceFlier,
  Unit_Enemy_AxeCavalry, Unit_Test_Dummy, Unit_Test_Dummy_Weak（弱ダミーHP12・集中攻撃の検証用）,
  Unit_Player_Monk（味方修道士・杖ユニット。Phase 25）,
  武器28本＋W_Live（杖・ライブ。Phase 25。消費0）※2026-10-24 に作り替え〔名前と数値は「メモリゲージで払う武器の合意仕様」の節の一覧。
  W_Sword＝鉄の剣・W_Lance＝鉄の槍・W_Axe＝鉄の斧・W_Bow＝鉄の弓・W_Tome＝初級魔導・W_Dragonslayer＝ドラゴンキラー・
  W_WindCutter＝風薙ぎの剣・W_DragonHammer＝ドラゴンアクス のようにファイル名は旧名のまま〕,
  Tool_Vulnerary（傷薬。道具・Phase 24）, Stage_Test, Stage_01, Stage_AITest_Path,
  Stage_AITest_Ambush, Stage_AITest_Pincer, Stage_AITest_Guard,
  Stage_AITest_Focus, TerrainTable（※Stage_AITest_Flight は 2026-10-24 に飛翔ごと削除）,
  ゲージ検証用（フェーズ28。すべて兵種＝剣闘士・武器＝鉄の剣。ファイル名の Mana はマナ時代のまま）:
  Unit_Test_Mana31（検証・技31＝開始時1本＋1）, Unit_Test_Mana_Mag20（毎ターン+2）,
  Unit_Test_Mana_Mag30（毎ターン+3）, Unit_Test_Speed50（1体目+25・2連続目+50・3連続目以降+100※2026-10-25・力20で弱敵を一撃）,
  Unit_Test_Enemy_Weak（HP1・守備0＝連続撃破を作る）,
  Unit_Test_Enemy_Tough（HP99・力0・守備5・待ち伏せ型＝倒せないので連鎖リセットと必殺技の確認用）,
  Stage_GaugeTest（12×10・全平地・ターン制限なし。y6に硬敵/弱敵6体/硬敵、y5にその真下へ味方7体。
  弱敵を3体連続撃破すると速さ50の覚醒が1本＋75、4体で2本＋75 貯まる※2026-10-25。旧版は5体連続でちょうど1本）,
  ※補給の検証用 Stage_SupplyTest は 2026-10-24 に補給ごと削除。
  武器の検証用（2026-10-24）: Stage_WeaponTest（「メモリゲージで払う武器の合意仕様」の節）と
  試験ユニット8体 Unit_Test_Memory_Sword/Lance/Axe/Critical/Support/Tome・Unit_Test_Sniper・Unit_Test_Archer_Longbow。
  Classes/ サブフォルダに兵種 ClassData 38種: Class_Tactician（軍師）〜Class_MaligKnight（魔竜騎兵）。
  飛行兵は Class_SkyKnight（白翼騎兵）・Class_FalconKnight（聖白翼騎兵）・Class_KinshiKnight（金鵄兵）・
  Class_DragonKnight（竜騎兵）・Class_WyvernLord（大竜騎兵）・Class_MaligKnight（魔竜騎兵）
  ※2026-10-24 に Class_PegasusKnight→Class_SkyKnight、Class_WyvernRider→Class_DragonKnight へ改名（GUID 不変。
  中の兵種名は同日さらに白翼騎兵・竜騎兵へ改めたが、ファイル名は据え置き）。
  ※Unit_Player_AxeCavalry / Unit_Enemy_AxeCavalry はファイル名こそ旧名だが中身は槍騎兵（兵種データ導入時に持ち替え））
シーン: Assets/_Game/Scenes/BattleScene.unity
絵: Assets/_Game/Art/Units/（マップ上のユニットの .aseprite。「兵種データの合意仕様」の節の「マップ上のユニットの絵」参照）
