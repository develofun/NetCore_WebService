# .Net Core로 웹 서비스 개발

```
개발 환경
 - OS: Window 10 (WSL2, Ubuntu 20.04)
 - IDE: Visual Studio Code
```

## 1. Web API 프로젝트 생성
 - Example 폴더 생성 >> ``` mkdir Example ```
 - 닷넷 커맨드를 이용해 프로젝트 생성 >> ``` dotnet new webapi ```

## 2. Swashbuckle을 이용한 API 자동화 문서 제공
 - Nuget 패키지 업데이트
```
    <PackageReference Include="Swashbuckle.AspNetCore" Version="5.5.1" />
    <PackageReference Include="Swashbuckle.AspNetCore.Annotations" Version="5.5.1" />
    <PackageReference Include="Swashbuckle.AspNetCore.Filters" Version="5.1.2" />
```

 - StartUp 설정 업데이트
```
    services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
        {
            Version = "v1",
            Title = ".Net Core Web Service API"
        });
    });
```
```
    app.UseSwagger();
    app.UseSwaggerUI(c => { c.SwaggerEndpoint("/swagger/v1/swagger.json", ".Net Core Web Service API V1"); });
```

## 3. MySQL, Dapper를 이용한 CRUD 처리
 - Nuget 패키지 업데이트
```
    <PackageReference Include="Dapper" Version="2.0.35" />
    <PackageReference Include="MySql.Data" Version="8.0.21" />
```

 - appsettings.json에 DB Connection 정보 추가
```
  "AppSettings": {
    "ConnectionString": "Server={Server};Port=3306;Database={DB};User={UserName};Password={Password};CheckParameters=False;AllowUserVariables=True;UseAffectedRows=True;CharSet=utf8mb4;SslMode=none;"
  },
```

 - DB Connection 설정 및 Transaction
```
    using var conn = new MySqlConnection(_connStr);
    await conn.OpenAsync();
    MySqlTransaction dbt = await conn.BeginTransactionAsync();

    try
    {
        await dbt.Connection.ExecuteAsync(
            "insert into user(`account`, `password`, `email`, `createTime`) values(@account, @password, @email, now());",
            new { user.Account, user.Password, user.Email }
        );
    }
    catch(Exception ex)
    {
        _logger.LogError(ex.StackTrace);
        await dbt.RollbackAsync();
        await conn.CloseAsync();
    }
```