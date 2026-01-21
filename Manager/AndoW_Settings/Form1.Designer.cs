namespace AndoWSettings
{
    partial class Form1
    {
        /// <summary>
        /// 필수 디자이너 변수입니다.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// 사용 중인 모든 리소스를 정리합니다.
        /// </summary>
        /// <param name="disposing">관리되는 리소스를 삭제해야 하면 true이고, 그렇지 않으면 false입니다.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form 디자이너에서 생성한 코드

        /// <summary>
        /// 디자이너 지원에 필요한 메서드입니다.
        /// 이 메서드의 내용을 코드 편집기로 수정하지 마십시오.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Form1));
            this.LogSubComboBox = new System.Windows.Forms.ComboBox();
            this.DeleteLogBtn = new System.Windows.Forms.Button();
            this.ViewLogBtn = new System.Windows.Forms.Button();
            this.LogComboBox = new System.Windows.Forms.ComboBox();
            this.label23 = new System.Windows.Forms.Label();
            this.SaveBtn = new System.Windows.Forms.Button();
            this.label27 = new System.Windows.Forms.Label();
            this.FTPPort = new System.Windows.Forms.TextBox();
            this.pictureBox1 = new System.Windows.Forms.PictureBox();
            this.label6 = new System.Windows.Forms.Label();
            this.aspect_ratio_chbox = new System.Windows.Forms.CheckBox();
            this.showipbtn = new System.Windows.Forms.Button();
            this.PasvMinPort = new System.Windows.Forms.TextBox();
            this.label4 = new System.Windows.Forms.Label();
            this.PasvMaxPort = new System.Windows.Forms.TextBox();
            this.label7 = new System.Windows.Forms.Label();
            this.pictureBox2 = new System.Windows.Forms.PictureBox();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox2)).BeginInit();
            this.SuspendLayout();
            // 
            // LogSubComboBox
            // 
            this.LogSubComboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.LogSubComboBox.FormattingEnabled = true;
            resources.ApplyResources(this.LogSubComboBox, "LogSubComboBox");
            this.LogSubComboBox.Name = "LogSubComboBox";
            // 
            // DeleteLogBtn
            // 
            resources.ApplyResources(this.DeleteLogBtn, "DeleteLogBtn");
            this.DeleteLogBtn.Name = "DeleteLogBtn";
            this.DeleteLogBtn.UseVisualStyleBackColor = true;
            this.DeleteLogBtn.Click += new System.EventHandler(this.DeleteLogBtn_Click);
            // 
            // ViewLogBtn
            // 
            resources.ApplyResources(this.ViewLogBtn, "ViewLogBtn");
            this.ViewLogBtn.Name = "ViewLogBtn";
            this.ViewLogBtn.UseVisualStyleBackColor = true;
            this.ViewLogBtn.Click += new System.EventHandler(this.ViewLogBtn_Click);
            // 
            // LogComboBox
            // 
            this.LogComboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.LogComboBox.FormattingEnabled = true;
            resources.ApplyResources(this.LogComboBox, "LogComboBox");
            this.LogComboBox.Name = "LogComboBox";
            this.LogComboBox.SelectedIndexChanged += new System.EventHandler(this.LogComboBox_SelectedIndexChanged);
            // 
            // label23
            // 
            resources.ApplyResources(this.label23, "label23");
            this.label23.Name = "label23";
            // 
            // SaveBtn
            // 
            resources.ApplyResources(this.SaveBtn, "SaveBtn");
            this.SaveBtn.ForeColor = System.Drawing.Color.Black;
            this.SaveBtn.Name = "SaveBtn";
            this.SaveBtn.UseVisualStyleBackColor = true;
            // 
            // label27
            // 
            resources.ApplyResources(this.label27, "label27");
            this.label27.ForeColor = System.Drawing.Color.Black;
            this.label27.Name = "label27";
            // 
            // FTPPort
            // 
            resources.ApplyResources(this.FTPPort, "FTPPort");
            this.FTPPort.Name = "FTPPort";
            // 
            // pictureBox1
            // 
            this.pictureBox1.BackColor = System.Drawing.Color.DarkGray;
            resources.ApplyResources(this.pictureBox1, "pictureBox1");
            this.pictureBox1.Name = "pictureBox1";
            this.pictureBox1.TabStop = false;
            // 
            // label6
            // 
            resources.ApplyResources(this.label6, "label6");
            this.label6.ForeColor = System.Drawing.Color.DarkOliveGreen;
            this.label6.Name = "label6";
            // 
            // aspect_ratio_chbox
            // 
            resources.ApplyResources(this.aspect_ratio_chbox, "aspect_ratio_chbox");
            this.aspect_ratio_chbox.Name = "aspect_ratio_chbox";
            this.aspect_ratio_chbox.UseVisualStyleBackColor = true;
            // 
            // showipbtn
            // 
            resources.ApplyResources(this.showipbtn, "showipbtn");
            this.showipbtn.Name = "showipbtn";
            this.showipbtn.Click += new System.EventHandler(this.showipbtn_Click);
            // 
            // PasvMinPort
            // 
            resources.ApplyResources(this.PasvMinPort, "PasvMinPort");
            this.PasvMinPort.Name = "PasvMinPort";
            // 
            // label4
            // 
            resources.ApplyResources(this.label4, "label4");
            this.label4.ForeColor = System.Drawing.Color.Black;
            this.label4.Name = "label4";
            // 
            // PasvMaxPort
            // 
            resources.ApplyResources(this.PasvMaxPort, "PasvMaxPort");
            this.PasvMaxPort.Name = "PasvMaxPort";
            // 
            // label7
            // 
            resources.ApplyResources(this.label7, "label7");
            this.label7.Name = "label7";
            // 
            // pictureBox2
            // 
            this.pictureBox2.BackColor = System.Drawing.Color.DarkGray;
            resources.ApplyResources(this.pictureBox2, "pictureBox2");
            this.pictureBox2.Name = "pictureBox2";
            this.pictureBox2.TabStop = false;
            // 
            // Form1
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.label7);
            this.Controls.Add(this.showipbtn);
            this.Controls.Add(this.aspect_ratio_chbox);
            this.Controls.Add(this.pictureBox2);
            this.Controls.Add(this.label6);
            this.Controls.Add(this.pictureBox1);
            this.Controls.Add(this.SaveBtn);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.label27);
            this.Controls.Add(this.PasvMaxPort);
            this.Controls.Add(this.PasvMinPort);
            this.Controls.Add(this.FTPPort);
            this.Controls.Add(this.LogSubComboBox);
            this.Controls.Add(this.DeleteLogBtn);
            this.Controls.Add(this.ViewLogBtn);
            this.Controls.Add(this.LogComboBox);
            this.Controls.Add(this.label23);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "Form1";
            this.SizeGripStyle = System.Windows.Forms.SizeGripStyle.Hide;
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox2)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private System.Windows.Forms.ComboBox LogSubComboBox;
        private System.Windows.Forms.Button DeleteLogBtn;
        private System.Windows.Forms.Button ViewLogBtn;
        private System.Windows.Forms.ComboBox LogComboBox;
        private System.Windows.Forms.Label label23;
        private System.Windows.Forms.Button SaveBtn;
        private System.Windows.Forms.Label label27;
        private System.Windows.Forms.TextBox FTPPort;
        private System.Windows.Forms.PictureBox pictureBox1;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.CheckBox aspect_ratio_chbox;
        private System.Windows.Forms.Button showipbtn;
        private System.Windows.Forms.TextBox PasvMinPort;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.TextBox PasvMaxPort;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.PictureBox pictureBox2;
    }
}

