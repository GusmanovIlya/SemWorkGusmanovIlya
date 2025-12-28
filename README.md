Семестровая работа #1 Гусманов Илья
Сайт с турами YouTravel.me

Необходимое ПО: .NET 8.0, Postgress 16
Для Docker: Docker, Docker Compose

Запуск через Docker:
1. Из корневой папки проекта:
```bash
docker compose up --build
```
2. Поднимутся две задачи: tours_db, tours_app
3. Сервера находится на адресах:
   Главная страница - http://localhost:1234
   Админ-панель - http://localhost:1234/admin (данные для авторизации: admin/12345)

Запуск вручную:
1. Создать базу данных
```bash
createdb tours_db
```
Затем вставка данных
```bash
psql -U postgres -d tours_db -f db/init.sql
```
2. Параметры подключения находятся в Server/settings.json
3. Конечный запуск
```bash
cd Server
```
```bash
dotnet build
```
```bash
dotnet run
```
4. Сервера находится на адресах:
   Главная страница - http://localhost:1234
   Админ-панель - http://localhost:1234/admin (данные для авторизации: admin/12345)
