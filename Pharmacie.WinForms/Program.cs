using Pharmacie.Infrastructure;
namespace Pharmacie.WinForms;
internal static class Program
{
 [STAThread] static void Main() { ApplicationConfiguration.Initialize(); var dir=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Pharmacie"); Directory.CreateDirectory(dir); var dbPath=Path.Combine(dir,"pharmacie.db"); var factory=new SqliteConnectionFactory(dbPath); new DatabaseInitializer(factory).InitializeAsync().GetAwaiter().GetResult(); var store=new PharmacieStore(factory); store.SeedAsync().GetAwaiter().GetResult(); using var login=new LoginForm(store); if(login.ShowDialog()==DialogResult.OK && login.AuthenticatedUser is not null) System.Windows.Forms.Application.Run(new MainForm(store,login.AuthenticatedUser,dbPath)); }
}
