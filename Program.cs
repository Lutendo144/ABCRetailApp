using ABCRetailApp.Helpers;

var builder = WebApplication.CreateBuilder(args);


builder.Services.AddControllersWithViews();


builder.Services.AddDistributedMemoryCache(); 
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30); 
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true; 
});

var app = builder.Build();


var connectionString = builder.Configuration["AzureStorage:ConnectionString"];
var employeeTable = builder.Configuration["AzureStorage:EmployeeTable"];


AzureTableInitializer.InitializeEmployeeTable(connectionString, employeeTable);
AzureTableInitializer.AddInitialEmployees(connectionString, employeeTable);


if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();


app.UseSession();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
