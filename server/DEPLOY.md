# PsyPath API на сервере

Сервер — виртуальная машина KomaruFag в облаке НГТУ (AlmaLinux 9.4, 1.7 ГБ памяти).

| Что | Где |
|---|---|
| Адрес для приложения | `http://217.71.129.139:6354` (публикация TCP → 172.17.27.105:5080) |
| Будущий адрес с HTTPS | `https://psypath.cloud.nstu.ru` (публикация «Веб-приложение»; на 2026-11-08 ещё не применилась) |
| SSH | `ssh -p 6197 root@217.71.129.139` или `ssh.cloud.nstu.ru:6696` |
| Файлы API | `/opt/psypath-api/app` |
| Настройки и секреты | `/opt/psypath-api/api.env` (права 600): строка подключения, ключ токенов |
| Контейнер | `psypath_api`, образ `mcr.microsoft.com/dotnet/aspnet:9.0`, сеть `baza_pg_network`, лимит 256 МБ |

API ходит в базу по имени контейнера `postgres_container` внутри сети Docker. Наружу база ему не нужна.

## Проверить

```bash
docker ps --filter name=psypath_api
docker logs --tail 50 psypath_api
curl -s http://127.0.0.1:5080/api/health
```

## Обновить

Собирать на сервере не нужно (мало памяти). На ПК:

```powershell
dotnet publish server\PsyPath.Api\PsyPath.Api.csproj -c Release -o publish
scp -P 6197 -r publish\* root@217.71.129.139:/opt/psypath-api/app/
```

На сервере:

```bash
docker restart psypath_api
```

## Запуск с нуля

```bash
docker run -d --name psypath_api --restart unless-stopped \
  --network baza_pg_network -p 5080:5080 \
  --env-file /opt/psypath-api/api.env \
  -v /opt/psypath-api/app:/app:ro,Z -w /app \
  --memory 256m \
  mcr.microsoft.com/dotnet/aspnet:9.0 dotnet PsyPath.Api.dll
```

## Telegram-бот

С этого сервера `api.telegram.org` закрыт, а Cloudflare открыт, поэтому бот здесь работает через свой Cloudflare Worker (`BOT_API_URL`) или `BOT_PROXY`. Подробности — `PsyPath.Bot/README.md`.

| Что | Где |
|---|---|
| Файлы бота | `/opt/psypath-bot/app` |
| Настройки | `/opt/psypath-bot/bot.env` (права 600): `BOT_TOKEN`, `BOT_API_URL` или `BOT_PROXY`, `PSYPATH_API_URL=http://psypath_api:5080`, `BOT_DATA=/data` |
| Входы пользователей | `/opt/psypath-bot/data/sessions.json` |
| Запуск и перезапуск | `/opt/psypath-bot/start.sh` — та же команда `docker run`, что ниже, и лог |
| Контейнер | `psypath_bot`, тот же образ `aspnet:9.0`, сеть `baza_pg_network`, лимит 128 МБ |

На ПК:

```powershell
dotnet publish server\PsyPath.Bot\PsyPath.Bot.csproj -c Release -o publish-bot
scp -P 6197 -r publish-bot\* root@217.71.129.139:/opt/psypath-bot/app/
```

На сервере, первый запуск:

```bash
mkdir -p /opt/psypath-bot/data
nano /opt/psypath-bot/bot.env   # вписать настройки
chmod 600 /opt/psypath-bot/bot.env
docker run -d --name psypath_bot --restart unless-stopped \
  --network baza_pg_network \
  --env-file /opt/psypath-bot/bot.env \
  -v /opt/psypath-bot/app:/app:ro,Z -v /opt/psypath-bot/data:/data:Z -w /app \
  --memory 128m \
  mcr.microsoft.com/dotnet/aspnet:9.0 dotnet PsyPath.Bot.dll
docker logs -f psypath_bot
```

Обновить: залить файлы и `docker restart psypath_bot`.
