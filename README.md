# Система расчёта учебной нагрузки кафедры ФиПМ ВлГУ

Настольное приложение для автоматического импорта учебных планов из файлов формата PLX, хранения данных в базе данных и расчёта числа ставок преподавателей с разбивкой по кафедрам и курсам.

## Требования

- Windows 10/11
- Microsoft SQL Server (любая редакция)
- [.NET 8.0 Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)

## Вариант 1 — запуск готового exe

1. Установить [.NET 8.0 Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)
2. Установить [SQL Server Developer Edition](https://www.microsoft.com/en-us/sql-server/sql-server-downloads) (бесплатно)
3. Открыть SSMS, выполнить файл `schema.sql` — создаст базу данных AcademicWorkload
4. Скачать `PlxParser.exe` из [Releases](../../releases)
5. Запустить `PlxParser.exe`

## Вариант 2 — сборка из исходников

1. Установить [Visual Studio 2022](https://visualstudio.microsoft.com/) с компонентом .NET desktop development
2. Установить [SQL Server Developer Edition](https://www.microsoft.com/en-us/sql-server/sql-server-downloads)
3. Клонировать репозиторий
4. Открыть `PlxParser.sln`
5. Нажать Ctrl+Shift+B для сборки
6. Открыть SSMS, выполнить файл `schema.sql`
7. Запустить приложение из Visual Studio (F5)

## Возможности

- Импорт учебных планов из PLX-файлов системы МИСИС
- Хранение данных в реляционной БД Microsoft SQL Server
- Расчёт ставок по всему плану (режим 3.1)
- Расчёт ставок по курсам (режим 3.2)
- Сравнительный анализ нескольких планов (режим 3.3)
