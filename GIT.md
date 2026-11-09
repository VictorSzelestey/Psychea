# Как заливать код
## Где работаю
alt — bedolagi73, alt2 — VictorSzelestey. В main напрямую не пишем, чужую ветку не трогаем.
## Как залить
Перед работой: `git switch alt` (или alt2), затем `git pull origin main`.
После работы: `git add -A`, `git commit -m "что сделал"`, `git pull origin alt`, `git push origin alt` (для alt2 — alt2).
Или напиши агенту `/git_com`, он сделает это сам.
## Как выпустить релиз
1. `git switch main`, `git pull origin main`, `git merge alt` (или alt2). Подними версию в PsyPath/PsyPath.csproj.
2. Задай AndroidKeyStore=true, AndroidSigningKeyStore, AndroidSigningKeyAlias, AndroidSigningKeyPass, AndroidSigningStorePass и выполни `dotnet publish PsyPath/PsyPath.csproj -f net9.0-android -c Release`.
3. Готовый файл `*-Signed.apk` положи в releases/Psychea.apk, номер версии и список изменений (`changelog`) впиши в releases/version.json, затем `git add -A`, `git commit -m "релиз X.Y.Z"`, `git push origin main`.
## Чего нельзя
- Заливать ключи, пароли, .env, bot.env, appsettings.Production.json: репозиторий виден всем.
- Создавать новый ключ подписи: телефоны откажутся ставить обновление.
- Выполнять `git push --force` и удалять ветки.
