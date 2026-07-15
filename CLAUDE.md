# TsumeTsume_Strategy

ファイアーエムブレムif暗夜をモデルにした 2D タクティクスSRPG。Unity 6 (6000.3.10f1) + URP。
作者はプログラミングほぼ初心者。日本語で、専門用語をかみ砕いて丁寧に説明すること。絵文字は使わない。

## 進捗状況（2026-07-16 時点）

バーティカルスライス完成（Phase 0〜7）、Phase 8（データ基盤刷新）、
Phase 9（前衛・後衛武器と新三すくみ、CombatRules による判定一元化）、
Phase 10（コマンドメニューUI・移動取り消し）、
Phase 11（救出システム）、Phase 12（輸送隊・輸送隊死亡で敗北）、
Phase 13（地形システム）、Phase 14（空中移動・飛翔、城壁地形、着陸コマンド）、
Phase 15（統合仕上げ: ターン制限・可変盤面・騎乗地形制限・戦闘予測・Stage_01）まで
Play 確認済み・コミット済み。Phase 8〜15 ロードマップは完走。
次は次期ロードマップの策定（最優先は敵AIの強化。作者合意済み）と Stage_01 のバランス調整。

全体計画（Phase 8〜15 ロードマップ）は
`/home/wawo/.claude/plans/tsumetsume-strategy-phase8-tsumetsume-st-elegant-parasol.md` 参照（WSL側）。
ゲーム根幹の仕様は `docs/コアシステム仕様書.md`（リポジトリ側を正とする。Desktop の原本は作者の編集用）。

## 確定済みの仕様（作者との合意。勝手に変えない）

- 命中は必中（命中・回避RNGなし）。ダメージ = max(0, 攻撃+武器威力+三すくみ補正-(防御+地形防御))
- 武器分類（WeaponCategory）: 剣・斧・槍＝前衛(Melee=0)、弓・魔法＝後衛(Ranged=1)。データ駆動（WeaponData.category）
- 三すくみ（Phase 9 で新式へ移行済み）: 前衛武器の攻撃のみ。剣>斧>槍>剣。
  有利側が攻撃 = +max(2, 攻撃側の技−防御側の速さ)、有利側が防御 = −max(2, 防御側の速さ−攻撃側の技)。
  後衛武器・武装無しは前衛から攻撃されると「武装無し扱い」で不利判定。後衛側の攻撃時は三すくみなし
- 後衛武器の射程: 移動後は最小射程（2マス）ちょうど、静止時（自マスクリック）は 2〜武器上限（弓3・魔道書2）
- 反撃は基本なし。CombatSystem.CanCounter() フックが唯一の拡張点（現状常に false）
- 挟撃: 近接攻撃時、敵を挟んだ反対側の隣マス(上下左右)に前衛武器の味方がいれば追加攻撃。
  参加した味方は行動を消費しない。遠距離・後衛武器の味方では不成立。
  ガード（無効化）: 防御側の隣接4マスに同陣営の「前衛武器の歩兵」がいると挟撃不成立。
  ガード役自身は守られない。行動済みでも有効。敵味方対称
- 戦闘の可否・補正判定は CombatRules に一元化。UIとAIは必ず同じ関数を呼ぶ。
  合計ダメージ予測は CombatSystem.PredictTotalDamage（挟撃・ガード込み。AI評価と将来の予測UIの共有窓口）
- 武装無しユニットには攻撃コマンドを出さない（CanAttack が false）
- データは ScriptableObject 中心（UnitData / WeaponData）。数値調整は作者がエディタで行う
- 座標は左下(0,0)、X右+、Y上+。グリッドは見た目(コード生成の四角)と論理データ(TileData[,])の二層
- 仮素材（色付き四角＋IMGUI）で進行中。本番素材への差し替えは後
- 能力値は7種（HP・力・魔力・技・速さ・守備・魔防）。物理=力vs守備、魔法=魔力vs魔防（地形防御は両方に加算）
- 兵種の階層: 歩兵／騎乗兵（騎兵・飛行兵）。輸送隊は騎兵の一種（2026-07-11 合意）。
  騎兵向けルールは UnitClass.IsCavalry()（騎兵+輸送隊）、騎乗全体は IsMounted() を使う
- Unit は Initialize で UnitData をコピーした「ランタイム能力値」を使う（Strength〜Move/Class/Weapon プロパティ経由。
  Data 直読みは Unit 内の窓口のみ。ユニット列挙は UnitRegistry（名簿）経由で、FindObjectsByType は使わない）
- 移動: 味方のマスは通過可・停止不可、敵のマスは侵入・通過不可。非アクティブ化＝盤上から外れる（占有も明け渡す）。
  飛翔中の例外は「飛翔の合意仕様」の節を参照
- 操作フロー（Phase 10〜）: 移動 → 行動メニュー（攻撃/待機）→ 対象選択 のFE型。
  キャンセルは右クリック/ESC で1段階ずつ戻る（対象選択→メニュー→移動取り消し→選択解除）。
  移動の取り消しは ActionContext.RevertMove（占有も復元）。待機はメニューから明示的に選ぶ
  （旧「敵以外クリックで待機」は廃止）。メニューはユニット右隣・画面端で左に回り込み
- マスのハイライトは HighlightKind の優先度付き多層管理
  （Selection > TargetChoice > AttackRange > MoveRange > 市松模様）。色設定は GridManager に集約

### 救出システムの合意仕様（Phase 11・2026-07-11 作者合意）

- 救出: 騎乗ユニットのみ。対象は隣接する同陣営の歩兵（騎乗・輸送隊は被救出不可）。
  行動済みの味方も救出できる。救出後は「使っていない移動力分の再移動 → 待機」のみ
- 救出中のペナルティなし。救出中でも攻撃可能。救出と同一行動での「降ろす」は不可
- 降ろす: 隣接する空きマスへ再配置。降ろされたユニットはそのターン行動済み。
  降ろしたら（代わりに降ろすも含め）そのユニットの行動は終了
- 引き受け: 空きのある騎乗ユニットが、隣の救出中の味方から貨物をもらう。
  直後にできるのは「降ろす/待機」のみ（引き受け自体では行動済みにならない）
- 代わりに降ろす: 歩兵専用。貨物が歩兵で「貨物の移動力 ≧ 自分の移動力」のときのみ
- 救出・引き受けを実行したら取り消し不能（選択解除も不可。必ず待機等で行動を終える）
- 運び手が倒されたら、貨物はその場（死亡マス）に降ろされて生存
- 格納中のユニットは盤上から除外（占有明け渡し・敵AIの対象外・GetUnits に出ない）が、
  生存数（CountAlive）には数える → 「救出中の騎兵＋格納歩兵」だけでも敗北しない
- 可否判定は RescueRules に一元化（CombatRules と同じ方針）

### 輸送隊の合意仕様（Phase 12・2026-07-11 作者合意）

- 輸送隊は容量4。騎乗ユニット（騎兵・飛行兵）も救出・引き受けできる（通常の騎乗兵は歩兵のみ）
- 誰も輸送隊を救出できない。輸送隊自身も他へ格納されない（乗り込みも不可）
- 入れ子救出は禁止：貨物を持っているユニットは救出・乗り込みの対象にならない（合意(a)）
- 乗り込む：輸送隊に隣接した歩兵・騎乗兵が自分から格納される。
  自分は行動終了、輸送隊の行動は消費しない
- 貨物が複数のとき、降ろす・引き受け・代わりに降ろすは貨物リスト（CargoListMenu）から
  対象を選ぶ。1体だけなら自動選択でリストは出ない
- 引き受けの種別制限：通常の騎乗兵が受け取れるのは歩兵の貨物のみ。輸送隊は何でも受け取れる
- **味方の輸送隊が倒されたら敗北（ゲームオーバー）**。貨物の有無は問わない。
  FE上級者向けに自由度より機能・挙動を守る方針（作者）。
  通常の騎乗兵の死亡は従来どおり（貨物はその場に降ろされて生存、ゲームは続く）
- 輸送隊は当面プレイヤー専用（敵AIは救出系を使わないため。合意(c)）
- 輸送隊は武器なしユニット（攻撃コマンドが出ない。Phase 9 の武装無し分岐の実地例）

### 地形の合意仕様（Phase 13・2026-07-11 作者合意）

- 地形は5種: 平地`.`（コスト1/防御+0）、森`F`（2/+1）、山`M`（3/+2）、砦`T`（1/+3）、壁`#`（通行不可）。
  数値・色は TerrainTable.asset の編集だけで調整できる（記号は terrainRows で使う1文字）
- 地形コストは当面全兵種共通 →（Phase 15 で兵種別コスト・通行制限を導入。「Phase 15 の合意仕様」の節が正）
- 兵種限定の通行制限（山は歩兵のみ等）は当面なし →（同上。Phase 15 で導入済み）
- 地形防御は物理・魔法の両方に加算（Phase 8 合意の継続。DamageBreakdown の「地形」項）
- ステージの地形は StageData の文字マップ terrainRows（リスト先頭=盤面の最上段の行）。
  行数・文字数の不一致や未知の記号は警告して平地扱い
- 地形の適用は BattleSetup.Start → GridManager.ApplyTerrain の流れ（シーン参照の追加は不要）
- 表示は地形色×市松の明暗（GridManager.checkerDarken）。ハイライトは Color.Lerp で
  地形色に混ぜる（highlightBlend）ので地形とハイライトが同時に判別できる。
  カーソル下マスの地形名/コスト/防御を画面左下に1行表示（IMGUI）
- 移動範囲計算はダイクストラ法（コスト差のある地形で最短経路を保証）。
  マス進入コストは MovementCalculator.GetMoveCost(unit, tile) に分離
  （Phase 14 で「飛翔中は常に1」を差し込む拡張点）

### 飛翔の合意仕様（Phase 14・2026-07-12 作者合意。ロードマップの初版案から作者改訂あり、本節が正）

- 「飛翔」は飛行兵専用コマンドで、**行動を消費しない**。非飛翔中ならいつでも使える（移動前・移動後・救出後も）。
  使用後は残り移動力ぶんの飛行移動が可能。飛翔後に使えるコマンドは救出・降ろす以外（攻撃は飛翔中の敵のみ）
- 持続は**発動ターンを含めて3ターン**（表示「飛x3→飛x2→飛x1」。自軍フェイズ開始ごとに減り、0で自動着地）。
  着地したターンは地上ユニットとして普通に行動できる。着地したターンの再飛翔も可（クールタイムなし）
- 「着陸」コマンド: 飛翔中の飛行兵が使える（飛翔を発動したターンは不可）。飛翔を解除して**即行動済み**。移動→着陸も可
- 飛翔中の移動: 全マス移動コスト1（地形無視）。屋内壁 `#` は飛行でも通過不可。
  城壁 `=`（Phase 14 新地形。地上は通行不可・地形防御+2）は飛行なら通過も停止も可。
  飛翔が切れたとき城壁の上にいたら、そのまま城壁の上に着地する（翌ターン歩いて降りられる。詰みなし）
- すり抜け: 飛翔中は敵のマスを通過できるが、**「飛翔状態の敵」と「対空武器（弓・魔法）装備の敵」のマスは通過不可**。
  地上ユニットは飛翔中の敵の下を通過できる。停止は誰もいないマスのみ（着地衝突は仕組み上起こらない）
- 戦闘: 飛翔中の相手を攻撃できるのは「飛翔中のユニット」か「後衛武器（対空）」のみ。飛翔中は地上の敵を攻撃できない。
  飛翔中は地形防御を受けない。判定は CombatRules.CanEngage に一元化（UI・敵AI・挟撃が共有）
- 挟撃: 飛翔中の敵を挟めるのは飛翔中の前衛味方のみ。地上の前衛歩兵ガードは飛翔中の防御側を守れない
- 救出との関係: 飛翔中は救出系に原則関与できない（実行も対象も不可。乗り込むも不可）。
  例外は「引き受け」で、**飛翔状態が同じ相手同士**（地上同士・空中同士）なら可。
  空中で引き受けた貨物は降ろせない（降ろすは地上でのみ表示）。貨物を抱えたまま飛翔は可（着地後に降ろせる）
- 取り消しは1段階ずつ: メニュー→飛翔後の再移動取り消し（飛翔した位置へ）→飛翔解除→移動取り消し→選択解除。
  行動確定前なら範囲外クリック・別ユニットクリックでも飛翔は取り消される
  （救出後など取り消し不能のときは飛翔の取り消しだけが起こり、選択は残る）。
  飛翔が確定するのは待機・攻撃・着陸などで行動を終えたとき
- 敵AIは飛翔コマンドを使わない（従来合意）。StageData の配置ごとに「開始時飛翔ターン数」を指定でき、
  検証用の敵飛行兵は99ターンで最初から飛んでいる（Stage_Test。数値はアセット編集で変更可）

### Phase 15 の合意仕様（統合仕上げ・2026-07-16 作者合意）

- 勝敗条件はテスト用ではなく本番システム。今回は**ターン制限のみ追加**
  （StageData.turnLimit。0=無制限。最終ターンの敵フェイズ終了時に勝敗未決なら敗北。
  判定は増加前に行うので表示が制限を超えない。制限中は「ターン n／上限」表示）。
  拠点到達などの勝利条件バリエーションは次期ロードマップへ先送り（作者合意）
- 兵種別地形（通行制限まで導入）: 騎乗ユニット（騎兵・輸送隊・地上の飛行兵=IsMounted）は
  森コスト3（歩兵2）、山は**通行不可**（歩兵のみ）。TerrainDef の mountedWalkable / mountedCost
  （0=共通コストと同じ）で地形ごとに調整できる。飛翔中は従来どおり全マスコスト1
- 降ろす・代わりに降ろす・死亡時解放の配置先も**貨物の兵種**で判定
  （騎乗の貨物は山へ降ろせない。TileData.IsWalkableFor / MoveCostFor が判定窓口）
- 盤面サイズはステージごとに可変（StageData.gridWidth / gridHeight。0以下なら GridManager の
  Inspector 値）。カメラは GridManager.FitCamera が自動フィット（余白は cameraMargin）
- 戦闘予測（BattleForecast・IMGUI右下）: 対象選択中に攻撃対象へマウスを重ねると
  ダメージ内訳・挟撃の追加ダメージ（またはガードで無効）・相手の残りHP見込み・撃破を表示。
  実戦と同じ DamageCalculator / CombatRules を使うので数値は必ず一致する
- 敵AI: 攻撃先・接近先の点数が同点なら地形防御の高いマスを優先（飛翔中は地形防御0扱い）。
  既知の限界: 接近はマンハッタン距離基準のため、城壁の向こうの敵が門へ回り込まないことがある
  （次期の敵AI強化で対応予定）
- 新ユニット5種: 味方斧兵・味方斧騎兵・敵魔道士・敵槍飛行兵・敵斧騎兵（能力値は初期案。作者が調整）
- 正式ステージ Stage_01: 14×12・ターン制限20・味方10体対敵12体。南が自軍、中央に山脈と森、
  北に城壁（門は敵弓兵が対空で防衛）。BattleScene の BattleSetup は Stage_01 を参照中
  （Stage_Test に戻すのも Inspector の差し替えだけ）
- 次期ロードマップの最優先は**敵AIの強化**（作者の指名）

### Phase 8 以降の追加合意（2026-07-08）

- 武器分類: 剣・斧・槍＝前衛武器、弓・魔法＝後衛武器（WeaponData にカテゴリ欄を追加、データ駆動）
- 新しい三すくみ補正（Phase 9 で移行）: 有利側が攻撃 = +max(2, 攻撃側の技 − 防御側の速さ)、
  有利側が防御 = −max(2, 防御側の速さ − 攻撃側の技)。旧 ±1 固定は廃止
- 敵AIは新戦闘ルールに追随させる。救出・飛翔コマンドは当面敵は使わない
- メニューUIは IMGUI で簡易実装（uGUI 化は素材差し替え時期にまとめて）
- 必中（RNGなし）・反撃なし（CanCounter フック維持）・ScriptableObject データ駆動は継続
- 育成要素（レベル・経験値・成長率）はロードマップ外（将来枠）

## コード構成（Assets/_Game/Scripts/）

- Core/ … BattleController(操作の司令塔・7状態FSM: Idle/MoveSelect/CommandMenu/TargetSelect/UnitTargetSelect/TileSelect/CargoSelect。
  飛翔・着陸コマンドと飛翔の段階取り消しCancelFlightAndReturnもここ),
  ActionContext(1行動の文脈・移動取り消し・再移動予算・取り消し不能点Commit・飛翔の中間点MarkFlight),
  TurnManager(フェイズ・勝敗・ターン制限判定・簡易UI・フェイズ開始時の飛翔Tick),
  BattleSetup(StageDataを読んで初期配置・開始時飛翔の適用・配置マスの兵種チェック)
- Grid/ … GridManager(盤面・可変サイズApplyStageSize・カメラ自動フィットFitCamera・座標変換・
  多層ハイライト・地形適用ApplyTerrain・地形情報の画面下表示※騎乗差分も表示),
  TileData(1マスの論理データ。IsWalkable/CanFlyOver/MoveCost/DefenseBonus に加え
  兵種別の IsWalkableFor/MoveCostFor),
  TerrainType(地形enum+TerrainDef定義。城壁Rampart・飛行可否canFlyOver・騎乗の mountedWalkable/mountedCost),
  MovementCalculator(ダイクストラ移動範囲・味方すり抜け・兵種別コスト/通行制限・飛翔中はコスト1で対空敵のみすり抜け不可)
- UI/ … ActionMenu(IMGUI行動メニュー。Entryリスト駆動・BattleControllerが自動生成),
  CargoListMenu(貨物リスト選択。名前+HP表示・BattleControllerが自動生成),
  BattleForecast(戦闘予測パネル。対象選択中のマウスオーバーで表示・BattleControllerが自動生成)
- Units/ … Unit(盤上のユニット・ランタイム能力値・行動済み・格納Carried/IsCarried・飛翔IsFlying/FlightTurnsLeft),
  UnitClass(兵種enum+IsMounted),
  RescueRules(救出系コマンドの可否判定一元化・乗り込む判定・飛翔の制限と空中引き受け・
  貨物の兵種で判定する GetDroppableCargoes/GetDropCells・死亡時配置先探索FindReleaseCell),
  UnitRegistry(全ユニットの名簿・輸送隊死亡フラグPlayerTransporterLost), Faction(陣営enum)
- Combat/ … CombatSystem(攻撃解決・挟撃・ガード・反撃フック・PredictTotalDamage),
  CombatRules(射程・三すくみ・挟撃可否・飛翔の戦闘制限CanEngageの一元判定),
  DamageCalculator(物理/魔法分岐・DamageBreakdown内訳・飛翔中は地形防御なし), WeaponType, WeaponCategory(前衛/後衛)
- Data/ … UnitData(7能力値+兵種), WeaponData, StageData(初期配置+開始時飛翔initialFlightTurns+
  地形マップterrainRows+盤面サイズgridWidth/gridHeight+ターン制限turnLimit),
  TerrainTable(地形定義一覧・記号引き) (ScriptableObject定義)
- AI/ … EnemyAI(点数評価型・同点は地形防御の高いマス優先)
- Editor/ … UnitDataEditor, WeaponDataEditor(Inspectorの日本語表示。エディタ専用・ビルド非含有)

アセット実体: Assets/_Game/Data/
（Unit_Player, Unit_Enemy, Unit_Player_Archer, Unit_Player_Mage, Unit_Player_Cavalry,
  Unit_Player_Transport, Unit_Player_Flier, Unit_Player_AxeFighter, Unit_Player_AxeCavalry,
  Unit_Enemy_Lancer, Unit_Enemy_Archer, Unit_Enemy_Flier, Unit_Enemy_Mage, Unit_Enemy_LanceFlier,
  Unit_Enemy_AxeCavalry,
  W_Sword, W_Axe, W_Lance, W_Bow, W_Tome, Stage_Test, Stage_01, TerrainTable）
シーン: Assets/_Game/Scenes/BattleScene.unity

## 次にやること

Phase 8〜15 ロードマップは完走。次は:
- 次期ロードマップの策定（着手時に作者と項目・順序を合意する）。
  **最優先は敵AIの強化**（2026-07-16 作者指名）。候補: 経路ベースの接近（門へ回り込む）・
  集中攻撃・後衛の射程管理など。あわせて先送りした「勝利条件のバリエーション（拠点到達など）」も候補
- Stage_01 のバランス調整（作者のプレイ感想を聞いて数値・配置をアセット編集で反映）
- 注意: 旧ロードマップの Phase 14 の記述は初版案のまま。飛翔の確定仕様は本ファイルの
  「飛翔の合意仕様」の節が正（2ターン→3ターン等、作者改訂済み）

## 環境・運用の注意

- Unity は Windows 側で作者が操作。Claude は WSL からファイル編集と git のみ。
  Play モード確認は作者に依頼する（手順を番号付きで丁寧に案内する）
- シーン変更後は作者に Ctrl+S 保存を依頼してから git の状態を確認する
- 新 Input System のみ有効（activeInputHandler=1）。クリックは Mouse.current を使う
- 各フェーズ完了ごとに動作確認→コミットの小刻み進行。コミットメッセージは日本語
- Assets/_Recovery/ は Unity の自動生成物で .gitignore 済み
- 新規アセット追加時は Claude が .asset と .meta（GUID自動生成）をペアで作成する。
  .asset の YAML 編集は Unity を閉じた状態で行うのが安全（作者に事前確認する）
- 作者PCは Windows 11 の「スマートアプリコントロール」有効のため Burst の DLL がブロックされる
  （error code 4551）。ゲームコードは Burst 不使用なので Jobs > Burst > Enable Compilation を
  オフにして回避済み（2026-07-16。PCごとの設定で git には入らない）
