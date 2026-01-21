using System;
using System.Windows.Forms;
using System.IO;



namespace AndoWSettings
{
    public partial class FormLogView : Form
    {
        public FormLogView(string logFilePath)
        {
            //Thread.CurrentThread.CurrentUICulture = new CultureInfo("en-US");

            InitializeComponent();
            InitEventHandler();
            RefreshLogFile(logFilePath);
        }

        public void RefreshLogFile(string paramFilePath)
        {
            try
            {
                if (File.Exists(paramFilePath))
                {
                    listBox1.Items.Clear();
                    FileInfo file = new FileInfo(paramFilePath);
                    StreamReader stRead = file.OpenText();
                    while (!stRead.EndOfStream)
                    {
                        listBox1.Items.Add(stRead.ReadLine());
                    }
                    stRead.Close();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
          
        }

        public void InitEventHandler()
        {
            button3.Click += button3_Click;
        }

        void button3_Click(object sender, EventArgs e)
        {
            this.Close();
        }
    }
}
