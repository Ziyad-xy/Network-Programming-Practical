using System.Net.Sockets;
namespace cliente
{
    public partial class Form1 : Form
    {
        private string? selectedFilePath;

        private Button btnSelectFile = null!;
        private Button btnSend = null!;
        private Label lblStatus = null!;
        private ProgressBar progressBar = null!;

        public Form1()
        {
            InitializeComponent();
            BuildUI();
        }

        private void BuildUI()
        {
            this.Text = "File Compression Client";
            this.Size = new Size(430, 220);
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;

            btnSelectFile = new Button
            {
                Text = "Select File",
                Location = new Point(20, 20),
                Size = new Size(120, 35)
            };
            btnSelectFile.Click += BtnSelectFile_Click;

            btnSend = new Button
            {
                Text = "Send & Compress",
                Location = new Point(155, 20),
                Size = new Size(240, 35),
                Enabled = false
            };
            btnSend.Click += BtnSend_Click;

            lblStatus = new Label
            {
                Text = "Ready. Please select a file.",
                Location = new Point(20, 75),
                Size = new Size(380, 50)
            };

            progressBar = new ProgressBar
            {
                Location = new Point(20, 135),
                Size = new Size(380, 18),
                Style = ProgressBarStyle.Marquee,
                Visible = false
            };

            this.Controls.AddRange(new Control[]
                { btnSelectFile, btnSend, lblStatus, progressBar });
        }

        private void BtnSelectFile_Click(object? sender, EventArgs e)
        {
            using OpenFileDialog ofd = new OpenFileDialog { Filter = "All Files (*.*)|*.*" };
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                selectedFilePath = ofd.FileName;
                long size = new FileInfo(selectedFilePath).Length;
                lblStatus.Text = $"Selected: {Path.GetFileName(selectedFilePath)}  ({size:N0} bytes)";
                btnSend.Enabled = true;
            }
        }

        private async void BtnSend_Click(object? sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(selectedFilePath)) return;

            btnSend.Enabled = false;
            btnSelectFile.Enabled = false;
            progressBar.Visible = true;

            try
            {
                using TcpClient tcp = new TcpClient("127.0.0.1", 8080);
                using NetworkStream stream = tcp.GetStream();

                byte[] fileData = await File.ReadAllBytesAsync(selectedFilePath);
                long fileSize = fileData.Length;

                lblStatus.Text = "Sending file size...";
                await stream.WriteAsync(BitConverter.GetBytes(fileSize));

                lblStatus.Text = "Sending file data...";
                await stream.WriteAsync(fileData);

                lblStatus.Text = "Waiting for compressed file size...";
                byte[] SizeBuf = new byte[8];
                await stream.ReadExactlyAsync(SizeBuf, 0, 8);
                long compressedS = BitConverter.ToInt64(SizeBuf, 0);

                lblStatus.Text = "Receiving compressed file...";
                byte[] compressed = new byte[compressedS];
                int received = 0;
                while (received < compressedS)
                {
                    int chunk = await stream.ReadAsync(
                        compressed, received, (int)compressedS - received);
                    if (chunk == 0) throw new Exception("Connection closed before file was fully received.");
                    received += chunk;
                }

                using SaveFileDialog sfd = new SaveFileDialog
                {
                    Filter = "GZip Files (*.gz)|*.gz",
                    FileName = Path.GetFileNameWithoutExtension(selectedFilePath) + ".gz"
                };

                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    await File.WriteAllBytesAsync(sfd.FileName, compressed);
                    double ratio = 100.0 * compressedS / fileSize;
                    MessageBox.Show(
                        $"Done!\n\nOriginal:   {fileSize:N0} bytes\n" +
                        $"Compressed: {compressedS:N0} bytes\n" +
                        $"Ratio:      {ratio:F1}%",
                        "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    lblStatus.Text = $"Saved: {Path.GetFileName(sfd.FileName)}";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                lblStatus.Text = "Error occurred.";
            }
            finally
            {
                btnSend.Enabled = true;
                btnSelectFile.Enabled = true;
                progressBar.Visible = false;
            }
        }
    }
}