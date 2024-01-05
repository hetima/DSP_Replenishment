# Replenishment 

Mod for Dyson Sphere Program. Needs BepInEx.

Take out building items stored in the initial planet (the first planet landed on) from other planet.  

Right-clicking the building tool icon (or icon on Replicator window) will move the corresponding item stored in the initial planet's storage to the inventory. Storage with an outgoing sorter attached will be ignored. One stack amount is moved at a time.  
The following will be added to the building tool to make use of this feature. 
- Logistics drone and Logistics vessel and Logistics Bot
- each types of fuel rods and Space warper
- Accumulator(full)

Unlimited access to items regardless of stock in sandbox mode.

## Configuration

Replenishment has some settings depend on BepInEx (file name is `com.hetima.dsp.Replenishment.cfg`).

|Key|Type|Default|Description|
|---|---|---|---|
|EnableOutgoingStorage|bool|false|Whether or not to enable picking items from storages with an outgoing sorter attached.|
|EnableSearchingAllPlanets|bool|false|Whether or not to enable picking items from storages on any planets.|
|EnableSearchingInterstellarStations|bool|false|Whether or not to enable picking items from interstellar stations.|
|EnableRightClickOnReplicator|bool|true|Enable right-click to replenish on Replicator window.|


## 説明

初期惑星(ゲーム開始時に降り立つ星、正確には衛星ですが)のストレージに保管している建築アイテムを他の惑星からも取り出せるようになります。  

建築ツールのアイコンを右クリックすると初期惑星のストレージに保管されている該当アイテムがインベントリに移動します。搬出ソーターが接続されたストレージは対象外です。1回につき1スタック分の量が移動します。  
この機能を利用するために以下のアイテムが建築ツールに追加されます。
- 物流ドローン と 物流船 と 物流ボット が輸送カテゴリに
- 各種燃料棒 と 空間歪曲器 がストレージカテゴリに
- 蓄電器(満充電) が電力カテゴリに

サンドボックスモードでは、在庫に関係なく無制限にアイテムが入手可能になります。


## Release Notes

### v1.1.0
- Enable right-click to replenish on Replicator window

### v1.0.9

- Support Dark Fog Update(0.10.28.20759)

### v1.0.8

- Added Logistics Bot to the building tool slot

### v1.0.7

- Added config `EnableSearchingAllPlanets` and `EnableSearchingInterstellarStations` (default is off)
- Fixed calculating Proliferator points when taking out items

### v1.0.5

- Added config `EnableOutgoingStorage` (default is off)

### v1.0.4

- Unlimited access to items regardless of stock in sandbox mode

### v1.0.3

- Temporary update for 0.9.24.11192

### v1.0.2

- Rebuild for 0.8.23.9808

### v1.0.1

- Rebuild for 0.8.22.9331
- Better screen effect for item acquisition

### v1.0.0

- Initial Release

