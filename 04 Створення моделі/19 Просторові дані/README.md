# Просторові дані

Просторові дані відображають фізичне розташування та форму об'єктів. Багато баз даних підтримують цей тип даних, тому їх можна індексувати та запитувати разом з іншими даними. Типові сценарії включають запити щодо об'єктів у межах заданої відстані від розташування або вибір об'єкта, межа якого містить задане розташування. EF Core підтримує зіставлення з просторовими типами даних за допомогою просторової бібліотеки NetTopologySuite.

## Встановлення

Щоб використовувати просторові дані з EF Core, потрібно встановити відповідний пакет підтримки NuGet. Вибір пакета залежить від використовуваного вами постачальника. Для Microsoft.EntityFrameworkCore.SqlServer це Microsoft.EntityFrameworkCore.SqlServer.NetTopologySuite

## NetTopologySuite
NetTopologySuite (NTS) – це просторова бібліотека для .NET. EF Core дозволяє зіставляти з просторовими типами даних у базі даних, використовуючи типи NTS у вашій моделі. Щоб увімкнути зіставлення з просторовими типами через NTS, викличте метод UseNetTopologySuite у конструкторі параметрів DbContext постачальника. Наприклад, у SQL Server ви б викликали його так.

```cs
options.UseSqlServer(
    @"Data Source=(localdb)\MSSQLLocalDB;Initial Catalog=WideWorldImporters;ConnectRetryCount=0",
    x => x.UseNetTopologySuite());
```
Існує кілька типів просторових даних. Вибір типу залежить від типів фігур, які ви хочете дозволити. Ось ієрархія типів NTS, які ви можете використовувати для властивостей у вашій моделі. Вони розташовані в просторі імен NetTopologySuite.Geometries.

* Geometry
    * Point
    * LineString
    * Polygon
    * GeometryCollection
        * MultiPoint
        * MultiLineString
        * MultiPolygon

CircularString, CompoundCurve та CurePolygon не підтримуються NTS.

Використання базового типу Geometry дозволяє властивості задати будь-який тип форми.

## Довгота та широта

Координати в NTS представлені у вигляді значень X та Y. Для представлення довготи та широти використовуйте X для довготи та Y для широти. Зверніть увагу, що це зворотний формат широти та довготи, у якому ви зазвичай бачите ці значення.

## Запит даних

Наступні класи сутностей можна використовувати для зіставлення з таблицями у зразку бази даних Wide World Importers.

```cs
using NetTopologySuite.Geometries;
using System.ComponentModel.DataAnnotations.Schema;

//...

[Table("Cities", Schema = "Application")]
public class City
{
    public int CityID { get; set; }

    public string CityName { get; set; }

    public Point Location { get; set; }
}

[Table("Countries", Schema = "Application")]
public class Country
{
    public int CountryID { get; set; }

    public string CountryName { get; set; }

    // Database includes both Polygon and MultiPolygon values
    public Geometry Border { get; set; }
}
```

Використання

```cs
static void Do()
{
    using var context = new ApplicationDbContextFactory().CreateDbContext(null);

    Console.WriteLine(context.Model.ToDebugString());

    CleanDatabase(context).Wait();

    City city = new City
    {
        CityName = "New York",
        Location = new NetTopologySuite.Geometries.Point(-74.0060, 40.7128) { SRID = 4326 }
    };
    context.Cities.Add(city);
    context.SaveChanges();

    Country country = new Country
    {
        CountryName = "United States",
        Border = new NetTopologySuite.Geometries.Polygon(
            new NetTopologySuite.Geometries.LinearRing(new[]
            {
                new NetTopologySuite.Geometries.Coordinate(-125.0, 24.0),
                new NetTopologySuite.Geometries.Coordinate(-66.0, 24.0),
                new NetTopologySuite.Geometries.Coordinate(-66.0, 49.0),
                new NetTopologySuite.Geometries.Coordinate(-125.0, 49.0),
                new NetTopologySuite.Geometries.Coordinate(-125.0, 24.0)
            })) { SRID = 4326 }
    };
    context.Countries.Add(country);
    context.SaveChanges();

}
Do();
```

У LINQ методи та властивості NTS, доступні як функції бази даних, будуть перетворені на SQL. Наприклад, методи Distance та Contains перетворюються в наступних запитах. Дивіться документацію вашого постачальника, щоб дізнатися, які методи підтримуються.

```cs
// Find the nearest city
var nearestCity = await db.Cities
    .OrderBy(c => c.Location.Distance(currentLocation))
    .FirstOrDefaultAsync();
```
```cs
// Find the containing country
var currentCountry = await db.Countries
    .FirstOrDefaultAsync(c => c.Border.Contains(currentLocation));
```

## Зворотне проектування

Просторові пакети NuGet також дозволяють здійснювати зворотне проектування моделей із просторовими властивостями, але вам потрібно встановити пакет перед запуском Scaffold-DbContext або dotnet ef dbcontext scaffold. Якщо ви цього не зробите, ви отримаєте попередження про те, що не знайдено зіставлення типів для стовпців, і стовпці будуть пропущені.

## SRID ігнорується під час операцій клієнта

NTS ігнорує значення SRID під час операцій. Він передбачає площинну систему координат. Це означає, що якщо ви вказуєте координати у вигляді довготи та широти, деякі значення, що оцінюються клієнтом, такі як відстань, довжина та площа, будуть у градусах, а не в метрах. Для отримання більш змістовних значень спочатку потрібно спроектувати координати в іншу систему координат за допомогою бібліотеки, такої як ProjNet (для GeoAPI).

Якщо операція обчислюється сервером EF Core через SQL, одиницю вимірювання результату визначатиме база даних. 

Ось приклад використання ProjNet для обчислення відстані між двома містами.

```cs
public static class GeometryExtensions
{
    private static readonly CoordinateSystemServices _coordinateSystemServices
        = new CoordinateSystemServices(
            new Dictionary<int, string>
            {
                // Coordinate systems:

                [4326] = GeographicCoordinateSystem.WGS84.WKT,

                // This coordinate system covers the area of our data.
                // Different data requires a different coordinate system.
                [2855] =
                    @"
                        PROJCS[""NAD83(HARN) / Washington North"",
                            GEOGCS[""NAD83(HARN)"",
                                DATUM[""NAD83_High_Accuracy_Regional_Network"",
                                    SPHEROID[""GRS 1980"",6378137,298.257222101,
                                        AUTHORITY[""EPSG"",""7019""]],
                                    AUTHORITY[""EPSG"",""6152""]],
                                PRIMEM[""Greenwich"",0,
                                    AUTHORITY[""EPSG"",""8901""]],
                                UNIT[""degree"",0.01745329251994328,
                                    AUTHORITY[""EPSG"",""9122""]],
                                AUTHORITY[""EPSG"",""4152""]],
                            PROJECTION[""Lambert_Conformal_Conic_2SP""],
                            PARAMETER[""standard_parallel_1"",48.73333333333333],
                            PARAMETER[""standard_parallel_2"",47.5],
                            PARAMETER[""latitude_of_origin"",47],
                            PARAMETER[""central_meridian"",-120.8333333333333],
                            PARAMETER[""false_easting"",500000],
                            PARAMETER[""false_northing"",0],
                            UNIT[""metre"",1,
                                AUTHORITY[""EPSG"",""9001""]],
                            AUTHORITY[""EPSG"",""2855""]]
                    "
            });

    public static Geometry ProjectTo(this Geometry geometry, int srid)
    {
        var transformation = _coordinateSystemServices.CreateTransformation(geometry.SRID, srid);

        var result = geometry.Copy();
        result.Apply(new MathTransformFilter(transformation.MathTransform));

        return result;
    }

    private class MathTransformFilter : ICoordinateSequenceFilter
    {
        private readonly MathTransform _transform;

        public MathTransformFilter(MathTransform transform)
            => _transform = transform;

        public bool Done => false;
        public bool GeometryChanged => true;

        public void Filter(CoordinateSequence seq, int i)
        {
            var x = seq.GetX(i);
            var y = seq.GetY(i);
            var z = seq.GetZ(i);
            _transform.Transform(ref x, ref y, ref z);
            seq.SetX(i, x);
            seq.SetY(i, y);
            seq.SetZ(i, z);
        }
    }
}
```
```cs
var seattle = new Point(-122.333056, 47.609722) { SRID = 4326 };
var redmond = new Point(-122.123889, 47.669444) { SRID = 4326 };

// In order to get the distance in meters, we need to project to an appropriate
// coordinate system. In this case, we're using SRID 2855 since it covers the
// geographic area of our data
var distanceInDegrees = seattle.Distance(redmond);
var distanceInMeters = seattle.ProjectTo(2855).Distance(redmond.ProjectTo(2855));
```
