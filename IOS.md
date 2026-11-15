# Психея для iPhone

Ветка `iphone`: тот же код .NET MAUI, что и для Android, плюс `PsyPath/Platforms/iOS`. Минимум — iOS 15.

## Чем iPhone отличается от Android

- Музыка и звуки — AVAudioPlayer (`Platforms/iOS/Bgm.cs`, `Sfx.cs`), напоминание о серии — локальное уведомление (`Reminder.cs`).
- Автообновления из APK нет: новая сборка ставится поверх старой, данные сохраняются.
- API пока по http, поэтому в `Info.plist` разрешён http (NSAllowsArbitraryLoads). После перехода на https блок убрать.

## Сборка без Mac

Каждый push в ветку `iphone` запускает GitHub Actions (`.github/workflows/ios.yml`) на облачном Mac.
Готовый `Psychea-unsigned.ipa` — в Actions → запуск → Artifacts → `Psychea-ios`.

## Установка с Windows (бесплатный Apple ID)

1. Поставить iTunes и iCloud с сайта Apple (не из Microsoft Store) и Sideloadly (sideloadly.io).
2. Подключить iPhone кабелем, нажать «Доверять».
3. В Sideloadly перетащить `.ipa`, ввести Apple ID, нажать Start.
4. На iPhone: Настройки → Конфиденциальность и безопасность → Режим разработчика → вкл.
   Затем Настройки → Основные → VPN и управление устройством → доверять своему Apple ID.

Подпись бесплатного Apple ID действует 7 дней, на одном устройстве — до 3 таких приложений.

## Сборка на Mac

```sh
dotnet workload install maui-ios
dotnet build PsyPath/PsyPath.csproj -f net9.0-ios -t:Run -p:RuntimeIdentifier=ios-arm64
```

Для TestFlight нужен платный Apple Developer Program: сертификат Apple Distribution, профиль App Store,
`dotnet publish -f net9.0-ios -c Release -p:ArchiveOnBuild=true`, загрузка через Transporter.
Перед каждой загрузкой поднимать `ApplicationVersion` в `PsyPath.csproj`.
