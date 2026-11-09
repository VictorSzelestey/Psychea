# PsyPath API

ASP.NET Core (.NET 9) между приложением и PostgreSQL. Приложение ходит только сюда, пароль базы в APK не попадает.

## Запуск на ПК

1. Открой `PsyPath.sln` в Visual Studio, в списке запуска выбери `PsyPath.Api` и нажми F5. Откроется браузер с `/api/health`, там должно быть `{"status":"ok","schema":2}`.
2. Настройки лежат в `PsyPath.Api/appsettings.Development.json`: строка подключения к базе (пользователь `psypath_app`) и ключ токенов. Если файла нет, скопируй `appsettings.Development.example.json` под этим именем и впиши значения. Это секреты, на GitHub их не выкладывай (`.gitignore` уже исключает файл).
3. API слушает порт 5080 на всех адресах ПК.
   - Эмулятор Android подключается по `http://10.0.2.2:5080`. Это адрес по умолчанию в приложении.
   - Телефон в той же Wi-Fi сети подключается по `http://<IP ПК>:5080`. IP показывает `ipconfig` (IPv4). Адрес задаётся константой `DefaultUrl` в `PsyPath/Services/Api.cs`. Windows спросит про брандмауэр, разреши доступ для частной сети.

Чтобы запускать приложение и API одновременно: ПКМ по решению → «Настроить запускаемые проекты» → «Несколько запускаемых проектов» и выставить обоим «Запуск».

## База

Схема `psypath` создаётся скриптами `db/001_init.sql` и `db/002_top.sql`, по одному разу в порядке номеров.

## Запросы

| Метод | Путь | Кто |
|---|---|---|
| POST | `/api/auth/register`, `/api/auth/login` | все; не больше 10 попыток в минуту |
| POST | `/api/auth/refresh` — новый токен на 30 дней | вошедший |
| GET | `/api/me` | вошедший |
| GET / PUT | `/api/me/top` — топ-50 и своё место / `{ "hidden": true }` | вошедший |
| PUT | `/api/me/progress` | вошедший, тело — ProgressData |
| POST | `/api/me/lessons`, `/api/me/questionnaires` | вошедший |
| GET | `/api/content/versions`, `/api/content/{lessons\|questionnaires\|rating\|reference}` | все |
| GET | `/api/admin/stats`, `/api/admin/users` | админ |
| PUT | `/api/admin/content/{name}` — `{ "version": N, "data": {...} }` | админ |
| GET / POST | `/api/admin/content/{name}/history`, `/api/admin/content/{name}/rollback/{version}` | админ |

Ошибки приходят в виде `{ "error": "текст" }`.
