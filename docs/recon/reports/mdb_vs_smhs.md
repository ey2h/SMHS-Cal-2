# 汇宝参数库(59表) ↔ SMHS 数据库 对表结果

- 本项目库：SMHS_Bolt.db, SMHS_Glass.db, SMHS_Material.db, SMHS_Profile.db, SMHS_Region.db, SMHS_User.db
- 本项目表数：11
- 判定：完全覆盖(列重叠≥60%) **0** ／ 部分覆盖(25–60%) **0** ／ 缺失(<25%) **59**

## 缺失（汇宝有、你没有或几乎不对应）

| 汇宝表 | 行数 | 关键列 | 最佳猜测 | 列重叠 |
|---|---|---|---|---|
| 地区参数 | 634 | 100年基本风压、100年基本雪压、50年基本风压、50年基本雪压、αmax、地区名称 | SMHS_Bolt.db/Bolts | 0.00 |
| 多跨2 | 328 | 长度、代号、序号 | SMHS_Bolt.db/Bolts | 0.00 |
| 线材优化合并后数据 | 302 | 数量、下料长度、序号 | SMHS_Bolt.db/Bolts | 0.00 |
| 线材优化原始数据 | 302 | 数量、下料长度、序号 | SMHS_Bolt.db/Bolts | 0.00 |
| 钢管点玻龙骨 | 288 | 惯性矩、截面面积、抗弯矩、型材代号、序号 | SMHS_Bolt.db/Bolts | 0.00 |
| 多跨1 | 236 | 长度、代号、序号 | SMHS_Bolt.db/Bolts | 0.00 |
| 热工系数 | 144 | 材料名称、导热系数（W/m·K）、分组、干密度（Kg/立米） | SMHS_Bolt.db/Bolts | 0.00 |
| 立柱 | 95 | Ix惯性矩、Iy惯性矩、Wx1抵抗矩、Wx2抵抗矩、备注、壁厚 | SMHS_Bolt.db/Bolts | 0.00 |
| 吊顶主龙骨 | 92 | 惯性矩、抗弯矩、型材代号、序号 | SMHS_Bolt.db/Bolts | 0.00 |
| 采光顶主龙骨 | 88 | 惯性矩、截面面积、抗弯矩、型材代号、序号 | SMHS_Bolt.db/Bolts | 0.00 |
| 雨篷龙骨 | 84 | 惯性矩、截面面积、抗弯矩、型材代号、序号 | SMHS_Bolt.db/Bolts | 0.00 |
| 钢铝结合钢立柱 | 83 | Ix惯性矩、Iy惯性矩、Wx1抵抗矩、Wx2抵抗矩、备注、壁厚 | SMHS_Bolt.db/Bolts | 0.00 |
| 横框 | 67 | Ix惯性矩、Iy惯性矩、Wx1抵抗矩、Wx2抵抗矩、Wy1抵抗矩、Wy2抵抗矩 | SMHS_Bolt.db/Bolts | 0.00 |
| 采光顶副龙骨 | 63 | Ix惯性矩、Iy惯性矩、Wx抗弯矩、Wy抗弯矩、型材代号、序号 | SMHS_Bolt.db/Bolts | 0.00 |
| 吊顶副龙骨 | 63 | 惯性矩、抗弯矩、型材代号、序号 | SMHS_Bolt.db/Bolts | 0.00 |
| 横梃二（门窗用，含钢芯） | 60 | Ix惯性矩、Iy惯性矩、Wx1抵抗矩、Wx2抵抗矩、Wy1抵抗矩、Wy2抵抗矩 | SMHS_Bolt.db/Bolts | 0.00 |
| 立梃二（门窗用，含钢芯） | 53 | Ix惯性矩、Iy惯性矩、Wx1抵抗矩、Wx2抵抗矩、备注、截面积 | SMHS_Bolt.db/Bolts | 0.00 |
| 后锚固材料参数 | 48 | fstk，JGJ145规范用、fud,t，GB50367规范用、fud,v，GB50367规范用、fyk，JGJ145规范用、备注、材料 | SMHS_Bolt.db/Bolts | 0.00 |
| 螺栓1 | 48 | 备注、材质、公称直径、剪应力、拉应力、型号 | SMHS_Bolt.db/Bolts | 0.00 |
| 螺栓2 | 48 | 备注、材质、公称直径、剪应力、拉应力、型号 | SMHS_Bolt.db/Bolts | 0.00 |
| 螺栓3 | 48 | 备注、材质、公称直径、剪应力、拉应力、型号 | SMHS_Bolt.db/Bolts | 0.00 |
| 螺栓4 | 48 | 备注、材质、公称直径、剪应力、孔径、拉应力 | SMHS_Bolt.db/Bolts | 0.00 |
| 螺栓5 | 48 | 备注、材质、公称直径、剪应力、孔径、拉应力 | SMHS_Bolt.db/Bolts | 0.00 |
| 螺栓6 | 48 | 备注、材质、公称直径、剪应力、孔径、拉应力 | SMHS_Bolt.db/Bolts | 0.00 |
| 钢铝结合钢横框 | 45 | Ix惯性矩、Iy惯性矩、Wx1抵抗矩、Wx2抵抗矩、Wy1抵抗矩、Wy2抵抗矩 | SMHS_Bolt.db/Bolts | 0.00 |
| 螺栓7 | 36 | 备注、材质、公称直径、剪应力、拉应力、型号 | SMHS_Bolt.db/Bolts | 0.00 |
| 钢管点玻 | 30 | 长度、代号、序号 | SMHS_Bolt.db/Bolts | 0.00 |
| 铝单板 | 26 | 备注、厚度、抗剪强度、抗弯强度、型号 | SMHS_Bolt.db/Bolts | 0.00 |
| 幕墙自重参数 | 17 | 计算横梁时自重、立柱及主体自重、幕墙类别、软件初始默认值适用范围、序号 | SMHS_Bolt.db/Bolts | 0.00 |
| 雨蓬 | 16 | 长度、代号、序号 | SMHS_Bolt.db/Bolts | 0.00 |
| 热工数值 | 15 | 材料层号、材料厚度、材料名称、导热系数、修正系数 | SMHS_Bolt.db/Bolts | 0.00 |
| 复合搪瓷板 | 13 | 备注、厚度、抗剪强度、抗弯强度、型号 | SMHS_Bolt.db/Bolts | 0.00 |
| 单体搪瓷板 | 8 | 备注、厚度、抗剪强度、抗弯强度、型号 | SMHS_Bolt.db/Bolts | 0.00 |
| U型玻璃 | 7 | 板面抗弯矩、截面惯性矩Ix、截面惯性矩Iy、截面积、抗弯矩Wy、型材代号 | SMHS_Bolt.db/Bolts | 0.00 |
| 单元立柱二 | 7 | Ix惯性矩、Iy惯性矩、Wx1抵抗矩、Wx2抵抗矩、备注、壁厚 | SMHS_Bolt.db/Bolts | 0.00 |
| 单元立柱一 | 7 | Ix惯性矩、Iy惯性矩、Wx1抵抗矩、Wx2抵抗矩、备注、壁厚 | SMHS_Bolt.db/Bolts | 0.00 |
| 静力手册 | 7 | 变量参数、尺寸参数、序号 | SMHS_Bolt.db/Bolts | 0.00 |
| 钢铝结合铝立柱 | 6 | Ix惯性矩、Iy惯性矩、Wx1抵抗矩、Wx2抵抗矩、备注、壁厚 | SMHS_Bolt.db/Bolts | 0.00 |
| 单元横框二 | 4 | Ix惯性矩、Iy惯性矩、Wx1抵抗矩、Wx2抵抗矩、Wy1抵抗矩、Wy2抵抗矩 | SMHS_Bolt.db/Bolts | 0.00 |
| 单元横框三（中横框） | 4 | Ix惯性矩、Iy惯性矩、Wx1抵抗矩、Wx2抵抗矩、Wy1抵抗矩、Wy2抵抗矩 | SMHS_Bolt.db/Bolts | 0.00 |
| 单元横框一 | 4 | Ix惯性矩、Iy惯性矩、Wx1抵抗矩、Wx2抵抗矩、Wy1抵抗矩、Wy2抵抗矩 | SMHS_Bolt.db/Bolts | 0.00 |
| 钢铝结合铝横框 | 4 | Ix惯性矩、Iy惯性矩、Wx1抵抗矩、Wx2抵抗矩、Wy1抵抗矩、Wy2抵抗矩 | SMHS_Bolt.db/Bolts | 0.00 |
| 钛合金板 | 4 | 备注、泊松比、材料密度、弹性模量、厚度、抗剪强度 | SMHS_Bolt.db/Bolts | 0.00 |
| 不锈钢板 | 3 | 备注、厚度、抗剪强度、抗弯强度、型号 | SMHS_Bolt.db/Bolts | 0.00 |
| 钢板 | 3 | 备注、厚度、抗剪强度、抗弯强度、型号 | SMHS_Bolt.db/Bolts | 0.00 |
| 百叶叶片 | 1 | 惯性矩、截面面积、抗弯矩、型材代号、序号 | SMHS_Bolt.db/Bolts | 0.00 |
| 蜂窝铝板 | 1 | 备注、厚度、抗剪强度、抗弯强度、型号 | SMHS_Bolt.db/Bolts | 0.00 |
| 横梃一（门窗用） | 1 | Ix惯性矩、Iy惯性矩、Wx1抵抗矩、Wx2抵抗矩、Wy1抵抗矩、Wy2抵抗矩 | SMHS_Bolt.db/Bolts | 0.00 |
| 栏杆横龙骨 | 1 | 惯性矩Ix、惯性矩Iy、抗弯矩Wx、抗弯矩Wy、型材代号、序号 | SMHS_Bolt.db/Bolts | 0.00 |
| 栏杆横龙骨（钢芯） | 1 | 惯性矩Ix、惯性矩Iy、抗弯矩Wx、抗弯矩Wy、型材代号、序号 | SMHS_Bolt.db/Bolts | 0.00 |
| 栏杆竖龙骨 | 1 | 惯性矩、截面积、抗弯矩、型材代号、序号 | SMHS_Bolt.db/Bolts | 0.00 |
| 栏杆竖龙骨（钢芯） | 1 | 惯性矩、截面积、抗弯矩、型材代号、序号 | SMHS_Bolt.db/Bolts | 0.00 |
| 立梃一（门窗用） | 1 | Ix惯性矩、Iy惯性矩、Wx1抵抗矩、Wx2抵抗矩、备注、截面积 | SMHS_Bolt.db/Bolts | 0.00 |
| 铝镁硅合金板 | 1 | 备注、泊松比、材料密度、弹性模量、厚度、抗剪强度 | SMHS_Bolt.db/Bolts | 0.00 |
| 铝塑复合板 | 1 | 备注、厚度、抗剪强度、抗弯强度、型号 | SMHS_Bolt.db/Bolts | 0.00 |
| 铜板 | 1 | 备注、泊松比、材料密度、弹性模量、厚度、抗剪强度 | SMHS_Bolt.db/Bolts | 0.00 |
| 转角立柱 | 1 | Ix惯性矩、Iy惯性矩、Wx1抵抗矩、Wx2抵抗矩、Wy1抵抗矩、Wy2抵抗矩 | SMHS_Bolt.db/Bolts | 0.00 |
| 转角立柱（单元二） | 1 | Ix惯性矩、Iy惯性矩、Wx1抵抗矩、Wx2抵抗矩、Wy1抵抗矩、Wy2抵抗矩 | SMHS_Bolt.db/Bolts | 0.00 |
| 转角立柱（单元一） | 1 | Ix惯性矩、Iy惯性矩、Wx1抵抗矩、Wx2抵抗矩、Wy1抵抗矩、Wy2抵抗矩 | SMHS_Bolt.db/Bolts | 0.00 |

## 部分覆盖（列不一致，缺哪几列）

| 汇宝表 | 行数 | 汇宝列 | 对应表 | 你缺的列 |
|---|---|---|---|---|

## 已覆盖

| 汇宝表 | 汇宝行 | 对应表 | 你的行 | 列重叠 |
|---|---|---|---|---|

## 你独有（汇宝没有对应）

- SMHS_Bolt.db / Bolts (324 行, 9 列)
- SMHS_Glass.db / GlassTypes (0 行, 12 列)
- SMHS_Material.db / PanelMaterials (26 行, 11 列)
- SMHS_Material.db / CurtainWallWeights (17 行, 6 列)
- SMHS_Material.db / AnchorMaterials (48 行, 12 列)
- SMHS_Profile.db / ProfileColumns (95 行, 13 列)
- SMHS_Profile.db / ProfileBeams (67 行, 14 列)
- SMHS_Region.db / Regions (634 行, 10 列)
- SMHS_User.db / RevitSubmissions (0 行, 13 列)
- SMHS_User.db / Users (5 行, 18 列)
- SMHS_User.db / UsageLogs (0 行, 6 列)