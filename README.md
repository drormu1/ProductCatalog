# ProductCatalog

זהו פתרון ‎.NET‎ שמכיל את כל חלקי המערכת. הקובץ `ProductCatalog.sln` הוא **לב הפרויקט** — פותחים אותו ב-Visual Studio ומשם עובדים.

## דרישות
- Visual Studio 2022 (מומלץ)
- ‎.NET 8 SDK

## NuGet Restore ("Package Install")
בדרך כלל Visual Studio מבצע Restore אוטומטי בזמן ה-Build הראשון.
אם ה-Build נכשל בגלל חבילות חסרות, הריצו מהשורש:

```bat
dotnet restore ProductCatalog.sln
```

## Build
פתחו את `ProductCatalog.sln` ב-Visual Studio ובצעו **Build Solution** (`Ctrl+Shift+B`).

## Cache TTL (חשוב לדמו)
- ה-Cache מוגדר ל-`Sliding TTL` של **10 שניות**.
- המשמעות: הזמן נספר מחדש בכל גישה לפריט.
- כדי לראות `Expired` במוניטור, צריך להמתין יותר מ-10 שניות בלי גישה לאותו פריט.

## הרצה מקומית (מתיקיית runners)
אחרי שה-Build עבר בהצלחה, הריצו את שני קבצי ה-BAT הבאים (מתיקיית `runners`) בסדר הזה:

1) להרים את ה-API:

```bat
cd runners
RunAPI.bat
```

בסיום ההרצה אמור להיפתח דפדפן אוטומטית למסך המוניטור — מומלץ להשאיר אותו פתוח בחצי מסך, במקביל להרצת קבצי ה-BAT.

מסך מוניטור (Development):
- `http://localhost:5088/monitor`

2) להריץ תרחיש בדיקה (אחרי שה-API כבר רץ):

```bat
cd runners
run-http-scenario.bat
```

אם הכול תקין, `run-http-scenario.bat` אמור לרוץ עד הסוף בהצלחה ולהחזיר תגובות תקינות ל-`GET`/`POST`/`PUT`.
