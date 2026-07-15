# Налаштовувані операції міграції.

API MigrationBuilder дозволяє виконувати багато різних видів операцій під час міграції, але це далеко не вичерпний список. Однак, API також є розширюваним, що дозволяє вам визначати власні операції. Існує два способи розширення API: за допомогою методу Sql() або шляхом визначення власних об'єктів MigrationOperation. 

Для ілюстрації розглянемо реалізацію операції, яка створює користувача бази даних, використовуючи кожен підхід.
У наших міграціях ми хочемо дозволити написання наступного коду:

```cs
migrationBuilder.CreateUser("SQLUser1", "Password");
```

## Використання MigrationBuilder.Sql()

Найпростіший спосіб реалізувати користувацьку операцію – це визначити метод розширення, який викликає MigrationBuilder.Sql(). Ось приклад, який генерує відповідний Transact-SQL.

```cs
internal static class MigrationBuilderExtensions
{
    public static OperationBuilder<SqlOperation> CreateUser(
        this MigrationBuilder migrationBuilder,
        string name,
        string password)
        => migrationBuilder.Sql($"CREATE USER {name} WITH PASSWORD '{password}';");
}
```


На SQL Server використовуйте функцію EXEC, коли оператор має бути першим або єдиним у пакеті SQL. Вона також може знадобитися для усунення помилок синтаксичного аналізу в ідемпотентних сценаріях міграції, які можуть виникати, коли посилання на стовпці наразі не існують у таблиці.

Якщо ваші міграції потребують підтримки кількох постачальників баз даних, ви можете використовувати властивість MigrationBuilder.ActiveProvider. Ось приклад підтримки як Microsoft SQL Server, так і PostgreSQL

```cs
public static OperationBuilder<SqlOperation> CreateUser(
    this MigrationBuilder migrationBuilder,
    string name,
    string password)
{
    switch (migrationBuilder.ActiveProvider)
    {
        case "Npgsql.EntityFrameworkCore.PostgreSQL":
            return migrationBuilder
                .Sql($"CREATE USER {name} WITH PASSWORD '{password}';");

        case "Microsoft.EntityFrameworkCore.SqlServer":
            return migrationBuilder
                .Sql($"CREATE USER {name} WITH PASSWORD = '{password}';");
    }

    throw new Exception("Unexpected provider.");
}
```
Цей підхід працює лише за умови, що ви знаєте кожного постачальника, до якого застосовуватиметься ваша користувацька операція.

## Використання MigrationOperation

Щоб відокремити користувацьку операцію від SQL, можна визначити власну операцію міграції (MigrationOperation) для її представлення. Потім операція передається постачальнику, щоб він міг визначити відповідний SQL для генерації.

```cs
public class CreateUserOperation : MigrationOperation
{
    public string Name { get; set; }
    public string Password { get; set; }
}
```

За такого підходу методу розширення потрібно лише додати одну з цих операцій до MigrationBuilder.Operations.

```cs
    public static OperationBuilder<CreateUserOperation> CreateUser(
        this MigrationBuilder migrationBuilder,
        string name,
        string password)
    {
        var operation = new CreateUserOperation { Name = name, Password = password };
        migrationBuilder.Operations.Add(operation);

        return new OperationBuilder<CreateUserOperation>(operation);
    }
```

Такий підхід вимагає, щоб кожен постачальник знав, як генерувати SQL для цієї операції у своєму сервісі IMigrationsSqlGenerator. Ось приклад перевизначення генератора SQL Server для обробки нової операції.

```cs
public class MyMigrationsSqlGenerator : SqlServerMigrationsSqlGenerator
{
    public MyMigrationsSqlGenerator(
        MigrationsSqlGeneratorDependencies dependencies,
        ICommandBatchPreparer commandBatchPreparer)
        : base(dependencies, commandBatchPreparer)
    {
    }

    protected override void Generate(
        MigrationOperation operation,
        IModel model,
        MigrationCommandListBuilder builder)
    {
        if (operation is CreateUserOperation createUserOperation)
        {
            Generate(createUserOperation, builder);
        }
        else
        {
            base.Generate(operation, model, builder);
        }
    }

    private void Generate(
        CreateUserOperation operation,
        MigrationCommandListBuilder builder)
    {
        var sqlHelper = Dependencies.SqlGenerationHelper;
        var stringMapping = Dependencies.TypeMappingSource.FindMapping(typeof(string));

        builder
            .Append("CREATE USER ")
            .Append(sqlHelper.DelimitIdentifier(operation.Name))
            .Append(" WITH PASSWORD = ")
            .Append(stringMapping.GenerateSqlLiteral(operation.Password))
            .AppendLine(sqlHelper.StatementTerminator)
            .EndCommand();
    }
}
```
Замініть службу генератора міграцій SQL за замовчуванням на оновлену.

```cs
    private readonly string _connectionString = @"Data Source=(localdb)\MSSQLLocalDB;Initial Catalog=MyDB;ConnectRetryCount=0";
    protected override void OnConfiguring(DbContextOptionsBuilder options)
        => options
            .UseSqlServer(_connectionString)
            .ReplaceService<IMigrationsSqlGenerator, MyMigrationsSqlGenerator>();
```