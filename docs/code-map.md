# コード構成（段階4で消す仮置き）

## コード構成（Assets/_Game/Scripts/）

- Core/ … BattleController(操作の司令塔・16状態FSM: Idle/MoveSelect/CommandMenu/WeaponSelect/TargetSelect/UnitTargetSelect/TileSelect/CargoSelect/DamageCheck/ItemSelect/ItemActionSelect/ExchangeSelect/RefreshMenu/SystemMenu/SystemInfo/CantoSelect。
  ※飛翔・着陸コマンドと飛翔の段階取り消し（CancelFlightAndReturn・CanUseFlightCommand）は 2026-10-24 に撤廃・
  攻撃可能マス表示ComputeAttackCells※2026-10-25 から届くマスの計算本体は WeaponChoiceRules.GetAttackCellsFrom〔ここは移動範囲（通り抜けるだけの仲間のマスも含む※2026-10-28。ComputeHealCells も同じ）と自分のマスを除くだけ〕・
  持ち物コマンド（2段階サブメニュー: 装備/外す/道具使用/捨てる・ホバー詳細は右下固定）・杖コマンド（回復対象選択→ExecuteHeal・杖が届くマス表示ComputeHealCells）・
  交換コマンド（相手選択→2列の交換画面。左右共用のBuildExchangeColumn/TransferItem・列見出しBuildExchangeHeader・行動は消費せず1個目でCommit）・
  敵の行動範囲プレビューShowEnemyPreview※2026-10-28 から印の付いた敵にマウスを乗せている間だけ〔Update の最後で、
  Idle なら UpdateHoverPreview＝マウスの下の敵が DangerZoneView.IsMarked なら下見を出し、離れたら ClearEnemyPreview・
  乗せている敵が変わったときだけ描き直す・ユニット情報パネルの上は無視。敵のクリックは DangerZoneView.ToggleMark だけ・
  Idle の右クリック/ESC は何もしない〕・被ダメ確認の閲覧状態もここ・
  必殺技コマンド※フェーズ29〔武器・対象の選び方は攻撃と共通のEnterWeaponSelect(critical)・criticalPending〕・
  攻撃の武器選択→対象選択※2026-08-09〔EnterWeaponSelect(bool critical)（武器1本でも必ず一覧を出す）→
  BuildWeaponEntries（押せる＝その武器で狙える敵がいること。使えない武器は理由付きの灰色行）→
  行のホバーOnWeaponHovered→ShowWeaponTargets（その武器で狙える敵を赤く。行が変わったときだけ塗り直す）→
  EnterTargetSelect(WeaponData)（**その武器を装備**してから対象選択。forecast.Show〔盤上の隅〕＋unitInfo.Show〔右上〕）→
  敵クリックでExecuteAttack(Unit)（必殺技ならメモリ1本→CombatSystem.ResolveAttack〔武器のメモリは戦闘の後に払う〕→
  剣で倒したらTryEnterCanto→FinishAction）。
  **装備の切り替えは武器を選んだ瞬間・必殺技のメモリ消費は敵をクリックした瞬間**・
  必殺技ぶんの上乗せCriticalExtraCost※2026-10-24〔必殺技の武器一覧と対象さがしに渡す。「必殺技」コマンドを出す条件には
  CriticalMemoryCostを直接渡す〕・
  ハイライトはShowAttackTargetsに集約・避ける矩形の窓口AvoidPanelRects（戦闘予測〔盤上の隅〕とユニット情報〔右上〕の2枚）・
  攻撃対象FindAttackTargets（＝WeaponChoiceRules.FindAllTargets）と攻撃可能マスComputeAttackCellsは全武器ぶんを合算〕・
  再行動コマンド※フェーズ30〔Idleで行動済みの味方をクリック→EnterRefreshMenu／ExecuteRefreshで覚醒1本消費→SetActed(false)→EnterMoveSelect〕・
  コマンド一覧※2026-08-15〔Idleでユニットのいないマスをクリック→OpenSystemMenu（状況/設定/終了。ActionMenuを使い回し・
  出したマスsystemMenuCellを覚えてパネルから戻る先にする）→OpenSystemInfo(SystemInfoKind)は見るだけのSystemInfo／
  ExecuteEndTurnはturnManager.EndPlayerPhase()。キャンセルはパネル→一覧→閉じる〕・
  剣の撃破後1マス再移動※2026-10-24〔ExecuteAttackで使った武器を控え、剣で倒したらTryEnterCanto（勝利が決まった・
  動けるマスが無いときは入らない・入る前にCommit）→水色のマスでFinishCanto(cell)／自分のマス・右クリック・ESCでFinishCanto(null)〕・
  引き渡し・人交換※2026-10-26〔「引き渡し」→EnterGiveReceiverSelect→受け手をクリック→EnterGiveCargoSelect
  （貨物が2体以上＝輸送隊のときだけ一覧）→ExecuteGive／「人交換」→EnterSwapPartnerSelect→相手をクリック→
  EnterSwapGiveCargoSelect（自分の貨物）→EnterSwapTakeCargoSelect（相手の貨物）→ExecuteSwap。
  どちらも実行したらMarkGive＋Commit→ShowMoveRange（以後のメニューは待機だけ＝降ろすと同じ）・
  貨物リストShowCargoListの引数partnerで相手も赤く示す〕・
  持ち物の「装備」はWeaponMemoryRules.CanEquipByClassだけで押せる※2026-10-24〔メモリ不足でも装備できる・行に「（メモリ不足）」の目印〕),
  ActionContext(1行動の文脈・移動取り消し・再移動予算・取り消し不能点Commit※引数keepHasMovedで移動済みを引き継ぐ〔交換用・2026-10-24 から剣の撃破後1マス再移動でも〕。飛翔の中間点MarkFlightは 2026-10-24 に撤廃・
  引き渡し・人交換の後の印usedGive/MarkGive※2026-10-26〔以後は再移動→待機だけ＝usedDropと同じ扱い〕),
  TurnManager(フェイズ・勝敗・ターン制限判定・画面表示は決着の「勝利！／敗北…」だけ
  ※2026-08-15に左上のターン数表示と「ターン終了」ボタンを撤去〔コマンド一覧へ移設〕・
  クリア条件の文言BuildClearConditionLines※判定CheckGameEndの真下に置く＝食い違い防止・ターン制限の公開TurnLimit・
  敵AI思考ログの切替enemyAILog・敵フェイズ内の敵HasActed管理※フェイズ終了時に戻す・
  フェイズ開始時のメモリ加算/連鎖リセット※ターン1は初期値のまま。再行動フラグ戻しは 2026-10-25 に回数の制限ごと撤廃・
  再行動できる味方がいる間は自動でターンを終えない※フェーズ30・
  ※フェーズ32の自軍フェイズ開始時の補給（SupplyRules.TickRepair）とフェイズ開始時の飛翔・撃墜のTickは 2026-10-24 に撤廃),
  BattleSetup(StageDataを読んで初期配置・配置マスの兵種チェック〔飛行兵は城壁・山にも置ける〕)
- Grid/ … GridManager(盤面・可変サイズApplyStageSize・カメラ自動フィットFitCamera
  ※2026-08-15から「画面の左boardAreaWidth（既定0.58※2026-08-16に0.65から）・下boardBottomArea（既定0.10）を除いた矩形」に収める
  〔cam.rectは使わない＝領域外が塗られずゴミが残るため。orthographicSizeと位置だけで寄せる。
  画面サイズが変わったらUpdateで合わせ直す〕・座標変換・
  多層ハイライト※HighlightKindの優先順は Selection > TargetChoice > AttackRange > HealRange >
  MoveRange〔フェーズ32の補給圏 SupplyZone は 2026-10-24 に撤廃〕・
  敵の攻撃範囲の赤紫の層SetDangerZone※2026-10-25〔ハイライトとは別の層＝ResetAllHighlightsで消えない。
  混ぜるのはResolveColorの最後のMixDangerZoneで、ハイライトの無いマスと水色だけのマス・dangerZoneColor/dangerZoneBlend。
  ※2026-10-28 から塗りと一緒に紫の外枠も引く＝FillEnclosedWalls（囲まれた穴のうち誰も立てない壁だけ埋める）→DrawOutline・
  dangerZoneOutlineColor/dangerZoneOutlineThickness・引いた本数DangerZoneOutlineCount（ログ用）〕・
  印を付けた敵の範囲の薄い赤と外枠SetMarkedZone※2026-10-28 に SetMarkedOutline（2026-10-25）から改名〔塗りは範囲のマスそのもの
  ＝markedFill 層（ResolveColor でハイライトの無いマスだけ・赤紫より先に判定・markedFillColor/markedFillBlend）、
  外枠は FillHoles で内側の穴を全部埋めてから DrawOutline・markedOutlineColor/markedOutlineThickness。
  印の敵を染める色markedUnitTintもここ〕・別の層の差し替えは SetLayer が共通（SetDangerZone も使う）・
  外枠の線の描き方DrawOutline※2026-10-28 に当時の SetMarkedOutline から切り出し〔境目ごとに細長い四角（OutlineBar_n）を
  境目の真上に置く・線ひと組＝OutlineSet（入れ物・SpriteRenderer の使い回し・描画順）を紫と赤で1組ずつ・
  描画順は紫1・赤2＝マス0とユニット3の間・穴の判定は FindOutside が共通〕・
  地形適用ApplyTerrain・地形情報を**盤面の真下の帯に1行で表示**※2026-08-17
  〔文字はBuildTerrainInfo（地形名+移動コスト〔騎乗差分も〕+地形防御）を左詰め・座標は右端に右詰め・
  **横幅は盤面と同じ・高さは1行で固定**〔盤面の左下から動かない〕・
  極端に狭いウィンドウ用の保険としてUnitInfoLines.Truncateで「…」に丸める・
  描いた矩形の公開TerrainInfoRect〕・
  画面上の位置を返す窓口GetBoardScreenRect／GetCellsScreenRect(min,max)※後者は戦闘予測が
  「自分＋選べる相手が乗っている範囲」を測るのに使う。2026-08-17),
  TileData(1マスの論理データ。IsWalkable/CanFlyOver/MoveCost/DefenseBonus に加え
  兵種別の IsWalkableFor/MoveCostFor※2026-10-24 から飛行兵の決まり〔CanFlyOver・コスト1〕も先頭で受け持つ＝地形の扱いはここに一本化),
  TerrainType(地形enum+TerrainDef定義。城壁Rampart・飛行兵が入れるかcanFlyOver・騎兵の mountedWalkable/mountedCost),
  MovementCalculator(ダイクストラ移動範囲・味方すり抜け（敵のマスは誰も通れない）・兵種別コスト/通行制限〔TileData へ委ねる〕・
  通り抜けるだけのマス（仲間のマス）も返せる GetReachableCells の passThroughOut※2026-10-28〔範囲表示で水色にするため。省略可〕・
  通行判定はCanTraverseに集約・目標マス群への距離マップGetDistanceMap※敵AIの接近用・
  隣の1マスだけ動けるマスGetStepCells※2026-10-24〔剣の撃破後1マス再移動。地形コストは問わない〕)
- UI/ … MenuPlacement(盤上のユニットの隣に出るメニューの置き場所を決める共通処理※2026-08-17。
  MonoBehaviourではない静的クラス。Bounds＝盤面の画面矩形〔GridManagerを一度だけ探して覚える・
  RuntimeInitializeOnLoadMethodで入り直しのたびに戻す〕・Beside(center,right,w,h,out altX)＝
  右隣に置き右端をはみ出すなら左隣へ回り込んでBoundsへ押し込む・Clamp(rect)。
  **これらのメニューを盤面の外に出さないための窓口**＝以前は画面いっぱいが基準で、
  盤面の下側のマスで開くと地形情報の帯に重なっていた),
  ActionMenu(IMGUI行動メニュー。Entryリスト駆動・置き場所はMenuPlacement.Beside・BattleControllerが自動生成),
  CargoListMenu(貨物リスト選択。名前+HP表示・行ホバーの通知onHover※ユニット情報パネル用・
  置き場所はMenuPlacement.Beside・BattleControllerが自動生成),
  ItemListMenu(持ち物リスト＋行動サブメニューの2列同時表示・選んだ品の性能を画面右下に固定表示
  ※ホバー詳細には有効・特効の行も出る（2026-08-06）・ボタン幅は264px※2026-08-08に170→210、2026-10-24に210→264〔「シャイニングボウ（メモリ不足）」15文字〕・
  enabled で押せない行を灰色無効化・行ホバーの通知onHover※2026-08-09〔行番号を渡す。武器選択で
  「その武器で狙える敵」の赤表示の切替に使う。CargoListMenuのonHoverと同じ作法〕・
  置き場所はMenuPlacement※2026-08-17〔列1・交換の2列はBeside、行動サブメニューと
  AvoidPanels/ShiftVerticallyの上下左右の限界はBounds＝どれも盤面の外へ出ない〕・
  避ける矩形avoidRects※2026-08-09〔Show/SetActionsにSystem.Func<Rect[]>で渡す。
  AvoidPanelsが列1と行動サブメニューの両方を「反対の隣→縦にずらす」の順で逃がす
  （ShiftVerticallyは上下の余裕がある方・近い方へ）。**位置を決めるのは開いた最初の1回だけ**
  （placementResolved/placementOffset。毎フレーム決め直すとクリックの最中に一覧が動いて押せなくなる
  ＝上の合意仕様の【重要】を参照）。既存の持ち物・交換は渡さないので無改変〕・
  BattleControllerが自動生成。
  交換用の2列モードShowExchangeも持つ※両列とも持ち物リスト＋列見出し〔名前/HP・数/装備〕・ホバー詳細は両列有効・左右の回り込みは2列ぶんの幅で判定),
  BattleForecast(戦闘予測パネル・**盤上の4隅のどれか**※2026-08-17〔ChoosePanelRect＝
  「自分＋選べる相手が乗っている範囲」BuildUnitsRect（GridManager.GetCellsScreenRect）と
  地形情報の枠（GridManager.TerrainInfoRect）に一番重ならない隅を選ぶ・順番は左下/右下/左上/右上で固定〕。
  **幅220pxの縦長・行は折り返す**〔wordWrap＋CalcHeightで行ごとに高さを測る〕・
  ダメージの内訳は**計算式をやめて「攻撃力」「防御力」の項目**※2026-08-17
  〔攻撃力=与ダメージの下・防御力=三すくみの下。どちらもDamageBreakdownのAttackTotal/DefenseTotal＝
  三すくみを含まない合計。読み手は攻撃力＋三すくみ−防御力＝ダメージと足し引きできる〕。
  対象選択に入った瞬間に即表示・三すくみ専用行・
  被ダメ確認の逆向き予測ShowIncomingもここ・必殺技モード※フェーズ29〔倍率込みの数字・見出し「戦闘予測（必殺技）」〕・
  静止撃ち有利の表示※2026-07-30〔与ダメは実際のhasMovedで確定した数字＋理由の1行BuildStandingShotNote／
  被ダメは相手が後衛武器なら「移動時／静止時」の両方を並べるDrawIncomingBothCases〕・
  飛行兵の地形防御の注記BuildFlierTerrainNote※2026-10-24〔防御側の飛行兵が地形防御のあるマスにいるときだけ
  「※飛行兵は地形防御を受けない」。与ダメ・被ダメの両モード。以前の撃墜の注記BuildShootDownNoteは撤廃〕・
  有効・特効の注記BuildEffectiveNote※2026-08-06〔「※特効: 白翼（武器威力3倍）」／被ダメは「※相手の…」。
  属性名は相手自身のタグを正式名で〔竜騎兵〕※2026-10-24。弓魔法の移動時/静止時は倍率が同じなので1行だけ〕・
  武器の行BuildWeaponLine※2026-08-09〔「武器: 斬馬刀（消費4）」※2026-10-24 から耐久に代えてメモリ消費。対象選択に入る時点で武器は
  もう装備済みなので、装備中の武器から作るだけでよい〕・
  メモリの見込みBuildMemoryLines※2026-10-24〔「メモリ 14 → 9」（必殺技は1本も込み）・払った後にこの武器を払えなくなるなら
  「※攻撃後、この武器はメモリ不足で使えなくなる」〕・三すくみ逆転の注記BuildReversalNote※2026-10-24〔与ダメ・被ダメの両方〕・
  挟撃の行に支援役のメモリ消費・剣で倒し切るときの「※剣で倒すと、攻撃の後に1マス再移動できる」・
  描いた矩形の公開PanelRect※2026-08-09〔武器の一覧が避けるのに使う。
  描かなかったフレームはRect.zero〕・
  BattleControllerが自動生成),
  SystemInfoPanel(コマンド一覧の「状況」「設定」のパネル・右上※2026-08-15。SystemInfoKind(Status/Settings)で1枚を兼用・
  状況＝ターン数/フェイズ＋クリア条件（TurnManager.BuildClearConditionLines）＋両軍の数（UnitRegistry.CountAlive・未行動数も）
  ＋敵の攻撃範囲の表示中/非表示の1行※2026-10-25〔DangerZoneView.IsVisible を読む〕・
  設定＝「準備中」の案内だけ・見るだけでゲーム状態は変えない・BattleControllerが自動生成),
  DangerZoneView(敵の攻撃範囲の表示※2026-10-25。見るだけ・BattleControllerが自動生成。
  Rキーで敵全員の赤紫IsVisibleを切り替え〔ThreatMap.GetThreatCells→GridManager.SetDangerZone。紫の外枠も一緒に出る※2026-10-28・
  表示に切り替えた直後にマス数と外枠の線の本数をログに出す〕・
  印の付いた敵の一覧と付け外しToggleMark〔BattleControllerが何も選んでいないときの敵のクリックで呼ぶ。
  範囲はThreatMap.GetUnitRangeCellsの合算→GridManager.SetMarkedZone（薄い赤＋外枠）。印の敵はUnit.SetMarkTintで赤く・外したら白に戻す・
  印が付いているかを答える IsMarked※2026-10-28〔BattleController のマウスを乗せた下見用・倒れた敵は false〕・
  倒れた敵の印は自動で外す〕・描き直しは盤面の様子ReadBoardState〔全ユニットの識別番号・位置・生死・盤上にいるか＋
  敵の装備とメモリ〕を毎フレーム比べて変わったときだけ〔何も表示していない間は見張らない〕),
  UnitInfoPanel(ユニット情報パネル・右上・幅500px※2026-08-16に旧DebugUnitPanelと統合〔DebugUnitPanel.csは削除〕。
  常設モード＝自分でクリックを読み、盤上のユニットを出し続ける／空きマスで閉じる／敵フェイズ中も見られる・
  文脈モード＝相手選択中にBattleControllerがShow〔盤面モード＝候補をホバーで切替／単体モード＝指定1体※格納中の貨物用〕。
  Hideは文脈モードだけ畳み常設モードの表示に戻る・見出しの行は文脈モードのときだけ出す※常設は上へ詰める・
  いま出しているユニットの公開DisplayedUnit〔AIDebugPanelが読む〕・
  パネル内クリックは盤面にも表示切替にも使わない※ContainsScreenPointをパネル側とBattleControllerの両方で見る・
  描いた矩形の公開PanelRect・BattleControllerが自動生成),
  AIDebugPanel(敵AIの性格のデバッグ表示・**ユニット情報パネルの下に右詰め**〔PanelRectから位置を決める〕
  ※2026-08-16にユニット情報パネルの状態枠から切り出し。
  UnitInfoPanel.DisplayedUnitが敵のときだけ「敵兵　AI: 待ち伏せ型(未挑発)」を1行・
  文字はUnitInfoLines.BuildAIRow・枠はCalcSizeで中身ぴったり・見るだけ・BattleControllerが自動生成),
  UnitInfoView(ユニット情報を左右2カラムで描く共通部品※2026-08-16。MonoBehaviourではない静的クラス。
  レイアウトの切り替えは無い〔初版のUnitInfoLayoutは同日の修正案で廃止〕・
  左カラム＝顔枠〔空欄・残りの高さを全部もらう〕→名前/HP〔文字+3px・次の行とはTextRowGap=0で詰める〕→
  クラス名/移動力→ステータス枠〔見出し無し・中をぴったり二等分・左=力/魔力/技/速さ・右=守備/魔防
  ※武器は修正案2で削除〕→使用可能武器→兵種経験値バー・
  右カラム＝状態枠〔見出し無し〕→スキル枠→持ち物枠〔行の右端＝武器はメモリ消費「消費N」（払えないときは赤字MemoryShortageColor）・
  道具は残り回数「残り／最大」・名前はFitTextで実測して丸める〕→必殺技1行→メモリ/覚醒ゲージ・
  速さは鋼の武器などで下がっているとき「2(-3)」SpeedText※2026-10-24・
  高さは右カラムで決まりContentHeight(lineHeight)の式で出る※ユニットによらず一定・
  枠の高さBoxHeight(rows,lineHeight,hasTitle)※見出しを持つのはスキルと持ち物だけ・
  ホバー詳細HoverState〔描画中に乗っている項目を覚え、全部描いたあと直下に重ねて描く。
  中身があるのは持ち物と武器の行だけ・他は1行の空欄枠・下に入らなければ上へ開く〕),
  UnitInfoLines(ユニット情報の表示用の文字を組み立てる共通処理。
  ClassName/MoveLine※「移動力 : 6」〔2026-10-24 に地上／飛翔の2本立てを撤廃〕/UsableWeaponsLine※兵種の装備可能武器「使用可能武器: 剣・斧」/
  ItemLabel※持ち物の行の左（名前＋「（装備中）」）/ItemRightText※行の右端〔武器＝メモリ消費「消費N」（0は空）・
  道具＝残り回数「残り／最大」。2026-10-24 に ItemUsesText から改名〕/
  BuildStateRows〔状態枠の3行＝「状態 : 」＋前衛後衛（武器が無ければ非武装）／ガード〇×／救出中。
  ガードの行はGridManagerが要る・格納中の貨物はガードの行が「格納中」に置き換わる〕/
  BuildAIRow※AIDebugPanelが呼ぶ/
  BuildItemDetail※持ち物メニューのホバー詳細と同じ関数〔種類・前衛後衛・威力射程・メモリ消費・有効特効・
  装備付与（魔法・速さ−移動力・三すくみ逆転・兵種限定）※2026-10-24。属性名は短い呼び名〔竜騎兵→竜騎〕〕/
  Truncate〔溢れる文字列を「…」で切る〕。武器は EquippedWeapon を読む※メモリで払えない武器でも何を装備しているかは見せる),
  GaugeBars(ゲージバーを描く共通部品※フェーズ28。MonoBehaviourではない静的クラスで
  UnitInfoLinesと同じ位置づけ。UnitInfoViewが右カラム最下部で呼ぶ。
  緑＝メモリ/黄＝覚醒※2026-10-10〔色3つ MemoryColor/MomentumColor/BackColor と FillRect は盤上の表示と共用のため public〕・
  ラベル幅46px※2026-10-24〔マナ→メモリで3文字に〕・
  値10ごとに黒い目盛り・右に「n本 値／最大」・塗りはTexture2D.whiteTextureに色を掛ける・
  DrawSlim※2026-08-16〔仕様未定の兵種経験値バー用。ゲージの半分の太さ・同じラベル幅/カウンター幅を使うので
  同じ幅の行に描けば長さが揃う〕),
  GaugeRings(盤上のメモリ・覚醒のリングを描く共通部品※2026-10-10。静的クラスで Unit.OnGUI が呼ぶ。
  IMGUIに円弧の命令が無いので「直径・値・最大値・色」ごとに小さなテクスチャを作ってキャッシュし GUI.DrawTexture で貼る
  〔4×4点の判定でふちを滑らかに・512枚を超えたら破棄・Domain Reload なしの Play 用に静的変数を初期化〕。
  リングの直径は GetDiameter が唯一の計算元で、Unit.OnGUI の HP ゲージの長さもこれを使う※2026-10-11)
- Units/ … Unit(盤上のユニット・ランタイム能力値・行動済み・格納Carried/IsCarried・
  絵の色はRefreshColorの1か所で決める※2026-10-25〔元の色×印の色SetMarkTint（白＝染めない）×行動済みならRGBを半分。
  SetActedもここを通る〕・絵の描画順3※2026-10-28〔2026-10-25 に 1→2、10-28 に 2→3。マスとの間に紫の外枠（1）と
  印の赤い外枠（2）を通すため〕・
  移動力Move※2026-10-24 から1本〔2026-08-10〜10-24 の GroundMove/FlightMove の2本立てと、飛翔IsFlying・
  撃墜ShootDown/飛翔禁止FlightBlockedTurns は飛翔ごと撤廃〕・
  AI性格AIProfile/挑発済みIsProvoked※永続・所持品Items/装備EquippedWeapon※Weapon窓口〔2026-10-24: メモリで払えない武器も
  そのまま返す＝守りでは装備どおりに働く。攻撃の可否は CombatRules.TryGetAttackRange が見る〕・Equip/Unequip/破棄DiscardItem・
  受け渡しTransferItemTo※ItemSlotごと移すので残り回数を引き継ぐ・装備中なら自動で非武装になる・
  回復Heal※最大HPクランプ・道具使用UseTool・
  速さBaseSpeed/Speed/SpeedPenalty※2026-10-24〔読む側はSpeed窓口＝鋼の武器などで地上の移動力ぶん下がる・0未満にならない〕・
  ※フェーズ31の武器の耐久（EquippedSlot/IsEquippedWeaponWorn/ConsumeWeaponUse）は 2026-10-24 に撤廃・
  メモリ/覚醒ゲージMemory/Momentum※フェーズ28〔2026-10-24 にMana から改名。AddMemory/SpendMemory(量)※本数も崩して払う/
  AddMomentum/SpendMomentum・Initializeで技ぶんの初期メモリ＋敵だけ払えない初期装備を持ち替え〕・
  ※再行動の1ターン1回（UsedRefreshThisTurn/MarkRefreshUsed/ResetRefresh。フェーズ30）は 2026-10-25 に撤廃),
  GaugeState(ゲージ1本ぶんのランタイム状態: 値Value + 本数Stock + 上限Max/MaxStock。
  満タンでストックへ繰り上げ・超過分は繰り越し・ストック上限では頭打ち。ItemSlotと同じ素のC#クラス。
  合計Total＝本数×Max＋値・合計から払うSpend(量)※2026-10-24〔本数も崩して払い、本数と値を並べ直す〕),
  ItemSlot(所持品1つのランタイム状態: item + 残り回数UsesLeft ＋ 新品時の回数MaxUses/回数を持つかHasUses。
  回数を持つのは道具だけ※2026-10-24〔フェーズ31の武器の耐久と補給の回復Refillは撤廃〕),
  UnitClass(移動タイプenum: 歩兵0/騎兵1/飛行兵2/輸送隊3/重装歩兵4※末尾追加厳守
  ＋IsFoot〔徒歩=歩兵+重装歩兵〕/IsNormalInfantry〔通常歩兵のみ〕/IsMounted〔=!IsFoot〕/IsCavalry/IsFlier〔飛行兵。2026-10-24〕/DisplayName),
  ClassTier(階級enum: 下級/上級),
  ClassTag(特攻タグ[Flags]: 騎馬Horse 1/白翼WhiteWing 2/竜Dragon 4/重装Armor 8/竜騎兵DragonRider 16※末尾追加厳守。
  2026-10-24 に Pegasus→WhiteWing・SubDragon→DragonRider へ改名〔値は不変〕。包含関係と重ね合わせを持つ
  ClassTagExtensions〔判定用Expand=ユニットのタグを広げる〔竜騎兵→竜〕/表示用Cover=武器が狙うタグを広げる/
  Matches(unitTags,targetTags)/DisplayName(shortForm)〔shortFormは武器の説明用の短い呼び名＝竜騎兵→竜騎〕〕。
  包含を知っているのはここだけ＝兵種アセットには竜騎兵だけを付ける),
  RescueRules(救出系コマンドの可否判定一元化・乗り込む判定〔飛翔の制限と空中引き受けは 2026-10-24 に撤廃〕・
  貨物の兵種で判定する GetDroppableCargoes/GetDropCells・死亡時配置先探索FindReleaseCell・
  受け取れる兵種の共通判定CanReceive※2026-10-26〔引き受け・引き渡し・人交換で共通。空きの有無は見ない〕・
  引き渡しの受け手FindGiveReceivers/渡せる貨物GetGiveCargoes・人交換の相手FindSwapPartners※2026-10-26),
  ExchangeRules(交換の可否判定一元化※フェーズ26。隣接の相手探しFindExchangePartners・
  双方向の可否CanExchange・片方向CanGive・空きCanReceive・相手として成立するかIsExchangeablePair※同陣営/盤上),
  UnitRegistry(全ユニットの名簿・輸送隊死亡フラグPlayerTransporterLost), Faction(陣営enum)
- Combat/ … CombatSystem(攻撃解決・挟撃・ガード・反撃フック・PredictTotalDamage※引数attackerHasMovedは必須・
  必殺技の倍率適用※フェーズ29〔引数critical・本体の一撃だけ倍率で挟撃は等倍〕・
  静止撃ち有利の受け渡し※2026-07-30〔ResolveAttackの引数attackerHasMoved。プレイヤーはcontext.hasMoved、
  敵AIは移動前に控えた「立ち位置≠元の位置」〕・
  戦闘1回ごとの撃破通知→GaugeRules※フェーズ28〔挟撃のとどめも含む〕・
  メモリの支払い→WeaponMemoryRules.PayForAttack※2026-10-24〔攻撃側→挟撃の支援役の順。戦闘の判定が全部終わってから
  払う＝途中で払うと挟撃の成立判定や覚醒の配分がその一撃の中でずれる（フェーズ31の耐久の消耗と同じ位置）。
  使った武器は戦闘の前に控える・TryPincerの戻り値は追撃した味方〕。撃墜（2026-08-05）は 2026-10-24 に撤廃),
  WeaponMemoryRules(武器のメモリ消費の一元判定※2026-10-24。払えるかCanAfford(unit,weapon,extra)・
  兵種として装備できるかCanEquipByClass〔兵種の装備可能武器＋武器側の兵種限定。メモリは見ない〕・
  攻撃後の支払いPayForAttack〔ログつき〕・敵だけの自動の持ち替えAutoSwitchIfUnaffordable〔WeaponChoiceRules.CanChooseWeapon が
  false の陣営だけ。持ち替え先FindAutoEquip＝消費の低い順・同じなら持ち物の上〕・CostOf/MemoryOf。
  ※フェーズ31〜32の DurabilityRules・SupplyRules はこの日に削除),
  GaugeRules(メモリ・覚醒の数値と可否の一元判定※フェーズ28〜30。調整用の定数は先頭1ブロックに集約
  〔メモリ最大30・2本/魔力20で+2・30で+3/必殺技の倍率2.0・必殺技で払うメモリCriticalMemoryCost=30※2026-10-24/
  覚醒最大100・2本/撃破ごとに速さの50％・2連続目100％・3連続目以降200％※2026-10-25〕。
  InitialMemory/MemoryGainPerTurn/TickMemory/MomentumGain(unit,連続数)/NotifyBattleResult※連鎖の管理・撃破のたびに配る/ResetChain/
  CanUseCritical/CanRefresh/AnyoneCanRefresh。2026-10-24 に Mana→Memory へ改名。staticはRuntimeInitializeOnLoadMethodでリセット),
  EffectiveRules(有効・特効の一元判定※2026-08-06。窓口はGetMultiplier(weapon,defender)→1/2/3〔大きい方から見て
  当たったら返す＝重なっても一番大きい1つだけ〕・武器種そのものの相性はここの表が持つ〔槍→騎馬白翼・斧→重装竜騎兵の有効／
  弓→白翼・魔導書→竜の特効〕・個別の特効はWeaponData.effectiveTags・
  表示用のGetEffectiveTargets/GetSuperEffectiveTargets/GetMatchedTags〔相手自身のタグを返す※2026-10-24〕/
  BuildWeaponEffectLines〔竜を狙う武器は Cover で竜騎兵まで並べ、短い呼び名で出す※2026-10-24〕。
  対空の相手判定GrantsAntiAirAgainstは 2026-10-24 に撤廃),
  CombatRules(射程・三すくみ・挟撃可否の一元判定〔飛翔の戦闘制限CanEngage・対空IsAntiAirCapable・撃墜ShootsDownは 2026-10-24 に撤廃〕・
  後衛武器の静止撃ち有利※2026-07-30〔引数attackerHasMoved・既定true＝補正なし側・有利の式はAdvantageBonusで前衛と共有〕・
  攻撃できる立ち位置一覧GetAttackFromCells・挟撃の反対側マスGetPincerOppositeCell・
  仮定位置ガード判定WouldGuardAt・ガード役かどうかIsGuardingSomeone※Phase 18・
  2026-10-24: メモリで払えない武器には射程なしTryGetAttackRange〔攻撃の判定4か所がここでそろう〕・
  挟撃の支援役も払えることFindPincerAlly〔ガード役WouldGuardAtはメモリを見ない〕・
  三すくみ逆転FindTriangleReverser/TriangleCounterOf・剣の撃破後1マス再移動の判定GrantsStepAfterKill),
  StaffRules(杖の可否・回復対象・回復量の一元判定※Phase 25。HasStaffEquipped/TryGetStaffRange/FindHealTargets/HealAmount。
  武器はEquippedWeaponではなくWeapon窓口から読む※フェーズ31で差し替え〔当時は傷んだ杖を止めるため。耐久は撤廃〕),
  WeaponChoiceRules(攻撃時にどの武器で殴るかの一元判定※2026-08-09。候補GetAttackWeapons
  〔プレイヤー＝所持品の武器のうち杖でない・兵種が扱えるもの（メモリ不足も候補＝理由を見せる）＋装備中の武器は例外装備でも必ず／
  敵＝装備中の1本だけ〕・
  可否CanAttackWith・対象さがしFindTargets〔武器1本ぶん〕/FindAllTargets〔全武器の合算・重複なし〕・
  攻撃が届くマスGetAttackCellsFrom(grid,unit,stands)※2026-10-25〔全武器ぶん・立ち位置≠いまのマスなら移動した扱い・
  壁は除く。BattleController.ComputeAttackCellsとThreatMapが共有〕・
  使えない理由GetUnusableReason〔メモリ不足/届く敵なし。相手は引数に取らない＝武器を先に選ぶため〕・
  必殺技ぶんの上乗せ extraCost※2026-10-24〔「1本＋武器の消費」を払える武器だけ〕・
  武器を選べる陣営かCanChooseWeapon〔プレイヤーだけ＝自動で持ち替えない陣営の判定にも使う〕・
  **probe（お試し装備）WithWeapon**〔一時的にEquipして評価しfinallyで必ず戻す＝共有関数に武器引数を足さずに
  武器ごとの判定ができる。Phase 20の飛翔probe〔撤廃〕と同じ手口。Unit.Equip/Unequipに副作用を足すと壊れる〕),
  DamageCalculator(物理/魔法分岐・DamageBreakdown内訳〔ToLogString＝式＋合計※Console用・
  ToFormulaStringはその中で使う private／AttackTotal＝力or魔力＋武器威力・DefenseTotal＝守備or魔防＋地形防御
  ※どちらも三すくみを含まない合計で、戦闘予測の「攻撃力」「防御力」の項目に出る。2026-08-17。
  BaseTotal＝AttackTotal＋三すくみ−DefenseTotal〕・飛行兵は地形防御なし※2026-10-24〔以前は飛翔中だけ〕・
  武器威力の倍率mightMultiplier※2026-08-06〔有効2倍・特効3倍。EffectiveRulesから取る。
  武器威力にだけ掛かる＝必殺技の倍率とは別の段。ログは「威力5×3」〕・
  ダメージ倍率multiplier※フェーズ29〔BaseTotal＝倍率前/Total＝倍率後・切り捨て。
  structの既定値0は等倍として扱う。引数は既定1なので既存の呼び出しは挙動不変〕・
  攻撃側が移動したかattackerHasMoved※2026-07-30〔CombatRulesへ渡すだけ。既定true〕・
  魔法かどうかはWeaponData.DealsMagicDamage※2026-10-24〔魔法武器の印も見る〕),
  WeaponType(7種+武器種としての魔法ダメージ判定IsMagicDamage〔武器ごとの判定はWeaponData.DealsMagicDamage〕+日本語種類名DisplayName), WeaponCategory(前衛/後衛)
- Data/ … UnitData(7能力値+兵種データclassData+所持品items※上限InventoryCapacity=7。移動タイプ/移動力は
  EffectiveClass/EffectiveMove、初期所持品/装備は GetInitialItems/GetInitialWeapon で解決・武器不一致はOnValidateで警告・
  初期装備のメモリ消費が技を上回るとOnValidateで警告※2026-10-24〔味方＝開始時はその武器で攻撃できない／敵＝自動で持ち替わる〕),
  ClassData(兵種1種の定義: 名前・階級・移動タイプ・移動力・装備可能武器・特攻タグ・昇格先・
  マップの絵 playerMapSprite/enemyMapSprite＋GetMapSprite(faction)※2026-10-10〔未設定＝色付き四角〕),
  ItemData(所持品の抽象基底・DisplayName のみ),
  WeaponData(: ItemData・メモリ消費memoryCost・魔法武器magicDamage→DealsMagicDamage・速さ−移動力reducesSpeedByMove・
  三すくみ逆転の相手triangleReversalTags・兵種の限定exclusiveClasses→AllowsClass※いずれも2026-10-24〔既定は全部「何も持たない」＝
  既存アセット無傷。逆転を後衛武器に付けるとOnValidateで警告〕・
  この武器だけの特効effectiveTags※2026-08-06〔既定None＝既存アセット無傷〕
  ※フェーズ31〜32の耐久maxUses・標準装備isStandardIssueと、対空grantsAntiAir（2026-08-06〜）は2026-10-24に削除),
  ToolData(: ItemData・道具。healAmount/maxUses),
  StageData(初期配置+AI性格aiProfile+地形マップterrainRows+盤面サイズgridWidth/gridHeight+ターン制限turnLimit
  ※開始時飛翔initialFlightTurnsは2026-10-24に削除),
  TerrainTable(地形定義一覧・記号引き) (ScriptableObject定義)
- AI/ … EnemyAI(点数評価型・「考えるEvaluate→動かすExecute」の2段構え・
  思考の順番は 攻撃→待ち伏せゲート→ガード→接近（Phase 20 の飛翔の判断は 2026-10-24 に撤廃）・
  接近は距離マップの3段構え(経路→敵無視→直線距離)・日本語思考ログ・同点は地形防御の高いマス優先・
  性格ゲート※待ち伏せ型は挑発まで動かない＝挑発判定はTakeAction冒頭(被弾)と攻撃プラン成立時・
  挟撃のお膳立て+2とガード役潰し+50※Phase 18・守り配置TryPlanGuard※Phase 19・
  集中攻撃※Phase 21=敵フェイズ開始のBeginEnemyPhaseで目標focusTargetを1体選定・倒れたら再選定・
  静止撃ち有利の織り込み※2026-07-30〔ScoreAttackにhasMovedを渡す＝弓/魔法の敵はその場で撃つ手を高く評価。
  Executeは移動前にhasMovedを控える〕・
  staticはRuntimeInitializeOnLoadMethodでリセット。
  点数定数は先頭の1ブロックに集約: KillBonus 1000／FocusBonus 200／GuardBreakKillBonus 50／
  PincerSetupBonus 2〔AirThreatPenalty 1 は飛翔ごと撤廃〕),
  ThreatMap(盤面の危険の読み取り。挟撃されそうな味方と防げる最大ダメージの一覧
  FindPincerThreatenedAllies※self は盤面に居ない扱いで判定。Phase 19。
  陣営全員の攻撃が届くマスGetThreatCells・1体の範囲（移動＋本人のマス＋攻撃）GetUnitRangeCells※2026-10-25
  〔DangerZoneView 用。立ち位置の候補GetStandCells＝移動できるマス＋いまのマス＝EnemyAI.Evaluate と同じ。待ち伏せ型も区別しない〕・
  対空射程マスの集合GetAirThreatCells※Phase 20 は 2026-10-24 に撤廃),
  EnemyAIProfile(性格enum: 突撃型/待ち伏せ型。Phase 17),
  ActionPlan(行動予定の構造体+ActionKind。Stay/Approach/Attack/Guard※Phase 19。
  TakeOff/LandAndWait※Phase 20 は 2026-10-24 に撤廃)
- Editor/ … UnitDataEditor(兵種データ設定時は旧 兵種/移動力 欄を隠し「データ由来」を読み取り専用表示),
  WeaponDataEditor※2026-10-24 から全欄を表示（それまで特効・対空の欄が出ていなかった。対空の欄は同日に撤廃）, ToolDataEditor, ClassDataEditor(Inspectorの日本語表示。エディタ専用・ビルド非含有)
