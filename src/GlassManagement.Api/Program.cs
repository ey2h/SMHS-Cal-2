using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using GlassManagement.Core.Data;
using GlassManagement.Api.Data;
using GlassManagement.Api.Services;
using GlassManagement.Api.Services.RegionalStandard;
using GlassManagement.Api.Components;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);

// ===== 鏁版嵁搴?=====
builder.Services.AddDbContext<GlassDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// ===== 鐢ㄦ埛鏁版嵁搴擄紙鐙珛锛?=====
var userDbPath = Path.Combine(AppContext.BaseDirectory, "Data", "SMHS_User.db");
Directory.CreateDirectory(Path.GetDirectoryName(userDbPath)!);
builder.Services.AddDbContext<UserDbContext>(options =>
    options.UseSqlite($"Data Source={userDbPath}"));
builder.Services.AddScoped<AuthService>();

// ===== JWT 璁よ瘉 =====
var jwtKey = builder.Configuration["Jwt:Key"] ?? "SMHS_GlassManagement_JwtSecret_2024_SuperLongKeyForSecurity!";
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "GlassManagement";
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtIssuer,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });
builder.Services.AddAuthorization();

// ===== 鍦板尯鍙傛暟鏁版嵁鏈嶅姟 =====
builder.Services.AddSingleton<RegionDataService>();
builder.Services.AddSingleton<MaterialDataService>();
builder.Services.AddSingleton<GlassManagement.Api.Services.CalcBookPdfService>();
builder.Services.AddSingleton<GlassManagement.Api.Services.CalcBookWordService>();
builder.Services.AddSingleton<GlassManagement.Api.Services.CalcBookWordV2Service>();
builder.Services.AddSingleton<GlassManagement.Api.Services.AluminumPlateCalcBookService>();
builder.Services.AddSingleton<GlassManagement.Api.Services.UnitCurtainWallCalcBookService>();
builder.Services.AddSingleton<GlassManagement.Api.Services.PanelCalcBookService>();
builder.Services.AddSingleton<GlassManagement.Api.Services.DoorWindowCalcBookService>();
builder.Services.AddSingleton<GlassManagement.Api.Services.EnergySavingCalcBookService>();
builder.Services.AddSingleton<GlassManagement.Api.Services.PointSupportCalcBookService>();
builder.Services.AddSingleton<GlassManagement.Api.Services.WizardService>();
builder.Services.AddSingleton<GlassManagement.Api.Services.StaticCalcEngine.StaticCalcService>();
builder.Services.AddSingleton<GlassManagement.Api.Services.FEMService>();
builder.Services.AddSingleton<GlassManagement.Api.Services.SectionCalculationService>();

// ===== 鍦版柟瑙勮寖椋庤嵎杞借绠?=====
builder.Services.AddSingleton<WindLoadStandardFactory>();

// ===== Blazor =====
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
// Blazor璋冪敤鑷韩API锛堝悓杩涚▼锛?// 榛樿http://localhost:5188锛屽彲閫氳繃 appsettings.json 鐨?"ApiBaseUrl" 瑕嗙洊
var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? "http://localhost:5188";
builder.Services.AddHttpClient<GlassApiClient>(client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddHttpClient<WizardApiClient>(client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(60);
});

// ===== Controllers =====
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // 鏀寔 GlassBase 閫掑綊澶氭€?JSON 搴忓垪鍖?鍙嶅簭鍒楀寲
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        // 鍏佽 Infinity/NaN 搴忓垪鍖栵紝闃叉璁＄畻缁撴灉涓嚭鐜版棤绌峰€煎鑷?00閿欒
        options.JsonSerializerOptions.NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals;
    });

// ===== Swagger =====
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "SMHS Glass Curtain Calculation API",
        Version = "v1",
        Description = @"
## 功能模块

### 玻璃计算
单片玻璃、中空玻璃、夹胶玻璃、玻璃肋的强度和挠度验算

### 铝合金计算
横梁、立柱、铝板面板的强度、挠度和稳定性验算

### 石材计算
石材面板弯曲验算、挂件承载力验算

### 结构计算
预埋件承载力验算、结构胶粘接验算

### 计算书生成
8种计算书自动生成，支持Word输出

### 数据管理
玻璃材质、铝型材、石材、结构胶、预埋件、技术规范的CRUD管理

## 技术规范依据
- JGJ 102-2003 玻璃幕墙工程技术规范
- JGJ 113-2015 建筑玻璃应用技术规程
- JGJ 133-2001 金属与石材幕墙工程技术规范
- GB 50010 混凝土结构设计规范
",
        Contact = new OpenApiContact { Name = "幕墙技术部" }
    });

    // XML娉ㄩ噴
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
        c.IncludeXmlComments(xmlPath);

    // Tag鍒嗙粍鎺掑簭
    c.TagActionsBy(api => new[] { api.GroupName ?? api.ActionDescriptor.RouteValues["controller"] });
});

// ===== CORS =====
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    });
});

var app = builder.Build();

// ===== 鏁版嵁搴撳垵濮嬪寲 =====
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<GlassDbContext>();
    var created = await db.Database.EnsureCreatedAsync();
    if (!created)
    {
        // 鏁版嵁搴撳凡瀛樺湪锛屾鏌ユ槸鍚﹂渶瑕侀噸寤猴紙绉嶅瓙鏁版嵁鏇存柊鏃讹級
        // 濡傛灉绉嶅瓙鏁版嵁缂哄け锛屽垹闄ら噸寤?        if (!await db.GlassMaterials.AnyAsync())
        {
            await db.Database.EnsureDeletedAsync();
            await db.Database.EnsureCreatedAsync();
        }
    }
}

// ===== 鐢ㄦ埛鏁版嵁搴撳垵濮嬪寲 =====
using (var scope = app.Services.CreateScope())
{
    var userDb = scope.ServiceProvider.GetRequiredService<UserDbContext>();
    await userDb.Database.EnsureCreatedAsync();
    var authService = scope.ServiceProvider.GetRequiredService<AuthService>();
    await authService.EnsureAdminAsync();
}

// ===== 涓棿浠?=====
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "鐜荤拑骞曞绠＄悊绯荤粺 API v1");
    c.RoutePrefix = "swagger";
    c.DocumentTitle = "鐜荤拑骞曞绠＄悊绯荤粺 - Swagger";
});

app.UseCors();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();
app.MapControllers();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

