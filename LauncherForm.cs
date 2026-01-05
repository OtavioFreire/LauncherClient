using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.IO.Compression;
using System.Net.Http.Json;

namespace LauncherClient
{
    public partial class LauncherForm : Form
    {
        public const string cnpjNumber = "60.938.777/0001-23";

        public LauncherForm()
        {
            InitializeComponent();
        }

        private async void LauncherForm_Load(object sender, EventArgs e)
        {
            try
            {
                await ExecuteUpdateAsync();
            }
            catch (Exception ex)
            {
                OpenSystem();
                Application.Exit();
            }
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            int radius = 20;

            var path = new GraphicsPath();
            path.AddArc(0, 0, radius, radius, 180, 90);
            path.AddArc(Width - radius, 0, radius, radius, 270, 90);
            path.AddArc(Width - radius, Height - radius, radius, radius, 0, 90);
            path.AddArc(0, Height - radius, radius, radius, 90, 90);
            path.CloseAllFigures();

            this.Region = new Region(path);
        }

        private async Task ExecuteUpdateAsync()
        {
            CloseSystem();

            RefreshStatus("Validando versão...", 10);

            var localVersion = GetLocalVersion();
            var apiResponse = await GetServerVersionAsync(cnpjNumber, localVersion);

            if (!apiResponse.update)
            {
                OpenSystem();
                return;
            }

            RefreshStatus("Baixando arquivos...", 30);
            var zipPath = await DownloadUpdateAsync(apiResponse.urlDownload);

            RefreshStatus("Atualizando sistema...", 70);
            UpdateSystem(zipPath);

            RefreshStatus("Abrindo sistema...", 90);
            OpenSystem();
        }

        private void RefreshStatus(string text, int progress)
        {
            lblStatus.Text = text;
            progressBar.Value = progress;
            Application.DoEvents();
            Thread.Sleep(200);
        }

        private string GetLocalVersion()
        {
            var exePath = Path.Combine("C:\\Projetos\\Launcher\\ProgramaAtual", "SQOWatchGlobalVariables.exe");
            return FileVersionInfo.GetVersionInfo(exePath).FileVersion!;
        }

        private async Task<UpdateInfo> GetServerVersionAsync(string cnpjNumber, string localVersion)
        {
            using var client = new HttpClient();
            var url = $"http://localhost:5100/latest?cnpj={cnpjNumber}&version={localVersion}";
            return await client.GetFromJsonAsync<UpdateInfo>(url)
                   ?? throw new Exception("Resposta inválida da API");
        }

        private async Task<string> DownloadUpdateAsync(string urlDownload)
        {
            var filePath = Path.Combine(Path.GetTempPath(), "update.zip");

            using var client = new HttpClient();
            using var apiResponse = await client.GetAsync(urlDownload, HttpCompletionOption.ResponseHeadersRead);
            apiResponse.EnsureSuccessStatusCode();

            var total = apiResponse.Content.Headers.ContentLength ?? 1;
            using var stream = await apiResponse.Content.ReadAsStreamAsync();
            using var file = new FileStream(filePath, FileMode.Create, FileAccess.Write);

            var buffer = new byte[8192];
            long totalRead = 0;
            int read;

            while ((read = await stream.ReadAsync(buffer)) > 0)
            {
                await file.WriteAsync(buffer.AsMemory(0, read));
                totalRead += read;
                var progress = (int)(30 + (totalRead * 40 / total));
                RefreshStatus("Baixando arquivos...", progress);
            }

            return filePath;
        }

        private void UpdateSystem(string zipPath)
        {
            var mainPath = @"C:\Projetos\Launcher\ProgramaAtual";

            ZipFile.ExtractToDirectory(zipPath, mainPath, true);
        }

        private void CloseSystem() 
        {
            foreach (var proc in Process.GetProcessesByName("SQOWatchGlobalVariables"))
                proc.Kill();

            Thread.Sleep(1000);
        }

        private void OpenSystem()
        {
            var executePath = Path.Combine(@"C:\Projetos\Launcher\ProgramaAtual", "SQOWatchGlobalVariables.exe");

            var process = new ProcessStartInfo
            {
                FileName = executePath,
                WorkingDirectory = Path.GetDirectoryName(executePath)!,
                UseShellExecute = false
            };

            Process.Start(process);
            Application.Exit();
        }

    }
}
