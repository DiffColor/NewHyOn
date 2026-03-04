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
            this.SaveBtn = new System.Windows.Forms.Button();
            this.label27 = new System.Windows.Forms.Label();
            this.FTPPort = new System.Windows.Forms.TextBox();
            this.aspect_ratio_chbox = new System.Windows.Forms.CheckBox();
            this.showipbtn = new System.Windows.Forms.Button();
            this.PasvMinPort = new System.Windows.Forms.TextBox();
            this.label4 = new System.Windows.Forms.Label();
            this.PasvMaxPort = new System.Windows.Forms.TextBox();
            this.label7 = new System.Windows.Forms.Label();
            this.dataServerIpLabel = new System.Windows.Forms.Label();
            this.dataServerIpTextBox = new System.Windows.Forms.TextBox();
            this.messageServerIpTextBox = new System.Windows.Forms.TextBox();
            this.messageServerIpLabel = new System.Windows.Forms.Label();
            this.ftpRootPathLabel = new System.Windows.Forms.Label();
            this.ftpRootPathTextBox = new System.Windows.Forms.TextBox();
            this.SuspendLayout();
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
            this.FTPPort.TabStop = false;
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
            this.PasvMinPort.TabStop = false;
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
            this.PasvMaxPort.TabStop = false;
            // 
            // label7
            // 
            resources.ApplyResources(this.label7, "label7");
            this.label7.Name = "label7";
            // 
            // dataServerIpLabel
            // 
            resources.ApplyResources(this.dataServerIpLabel, "dataServerIpLabel");
            this.dataServerIpLabel.ForeColor = System.Drawing.Color.Black;
            this.dataServerIpLabel.Name = "dataServerIpLabel";
            // 
            // dataServerIpTextBox
            // 
            resources.ApplyResources(this.dataServerIpTextBox, "dataServerIpTextBox");
            this.dataServerIpTextBox.Name = "dataServerIpTextBox";
            this.dataServerIpTextBox.TabStop = false;
            // 
            // messageServerIpTextBox
            // 
            resources.ApplyResources(this.messageServerIpTextBox, "messageServerIpTextBox");
            this.messageServerIpTextBox.Name = "messageServerIpTextBox";
            this.messageServerIpTextBox.TabStop = false;
            // 
            // messageServerIpLabel
            // 
            resources.ApplyResources(this.messageServerIpLabel, "messageServerIpLabel");
            this.messageServerIpLabel.ForeColor = System.Drawing.Color.Black;
            this.messageServerIpLabel.Name = "messageServerIpLabel";
            this.messageServerIpLabel.TabStop = false;
            // 
            // ftpRootPathLabel
            // 
            this.ftpRootPathLabel.Font = new System.Drawing.Font("굴림", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(129)));
            this.ftpRootPathLabel.ForeColor = System.Drawing.Color.Black;
            this.ftpRootPathLabel.Location = new System.Drawing.Point(37, 185);
            this.ftpRootPathLabel.Name = "ftpRootPathLabel";
            this.ftpRootPathLabel.Size = new System.Drawing.Size(124, 26);
            this.ftpRootPathLabel.TabIndex = 120;
            this.ftpRootPathLabel.Text = "FTP Root Path :";
            this.ftpRootPathLabel.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // ftpRootPathTextBox
            // 
            this.ftpRootPathTextBox.Font = new System.Drawing.Font("굴림", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(129)));
            this.ftpRootPathTextBox.Location = new System.Drawing.Point(167, 185);
            this.ftpRootPathTextBox.Name = "ftpRootPathTextBox";
            this.ftpRootPathTextBox.Size = new System.Drawing.Size(190, 26);
            this.ftpRootPathTextBox.TabIndex = 121;
            this.ftpRootPathTextBox.TabStop = false;
            // 
            // Form1
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(494, 230);
            this.Controls.Add(this.ftpRootPathTextBox);
            this.Controls.Add(this.ftpRootPathLabel);
            this.Controls.Add(this.messageServerIpTextBox);
            this.Controls.Add(this.messageServerIpLabel);
            this.Controls.Add(this.dataServerIpTextBox);
            this.Controls.Add(this.dataServerIpLabel);
            this.Controls.Add(this.label7);
            this.Controls.Add(this.showipbtn);
            this.Controls.Add(this.aspect_ratio_chbox);
            this.Controls.Add(this.SaveBtn);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.label27);
            this.Controls.Add(this.PasvMaxPort);
            this.Controls.Add(this.PasvMinPort);
            this.Controls.Add(this.FTPPort);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "Form1";
            this.SizeGripStyle = System.Windows.Forms.SizeGripStyle.Hide;
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private System.Windows.Forms.Button SaveBtn;
        private System.Windows.Forms.Label label27;
        private System.Windows.Forms.TextBox FTPPort;
        private System.Windows.Forms.CheckBox aspect_ratio_chbox;
        private System.Windows.Forms.Button showipbtn;
        private System.Windows.Forms.TextBox PasvMinPort;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.TextBox PasvMaxPort;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.Label dataServerIpLabel;
        private System.Windows.Forms.TextBox dataServerIpTextBox;
        private System.Windows.Forms.TextBox messageServerIpTextBox;
        private System.Windows.Forms.Label messageServerIpLabel;
        private System.Windows.Forms.Label ftpRootPathLabel;
        private System.Windows.Forms.TextBox ftpRootPathTextBox;
    }
}
