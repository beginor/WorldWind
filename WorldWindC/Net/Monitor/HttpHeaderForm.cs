using System.ComponentModel;
using System.Windows.Forms;

namespace WorldWind.Net.Monitor {
	/// <summary>
	/// Displays details for one download.
	/// </summary>
	internal class HttpHeaderForm : Form {
		private TextBox description;
		/// <summary>
		/// Required designer variable.
		/// </summary>
		private Container components = null;

		/// <summary>
		/// Initializes a new instance of the <see cref= "T:WorldWind.Net.Monitor.ProgressDetailForm"/> class.
		/// </summary>
		/// <param name="item"></param>
		public HttpHeaderForm(DebugItem item) {
			InitializeComponent();

			this.Text = item.Url;
			this.description.Text = item.ToString();
			this.description.SelectionLength = 0;
		}

		protected override void OnKeyUp(KeyEventArgs e) {
			switch (e.KeyCode) {
				case Keys.Escape:
					Close();
					e.Handled = true;
					break;
				case Keys.F4:
					if (e.Modifiers
					    == Keys.Control) {
						Close();
						e.Handled = true;
					}
					break;
			}

			base.OnKeyUp(e);
		}

		/// <summary>
		/// Clean up any resources being used.
		/// </summary>
		protected override void Dispose(bool disposing) {
			if (disposing) {
				if (components != null) {
					components.Dispose();
				}
			}
			base.Dispose(disposing);
		}

		#region Windows Form Designer generated code
		/// <summary>
		/// Required method for Designer support - do not modify
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent() {
			this.description = new System.Windows.Forms.TextBox();
			this.SuspendLayout();
			// 
			// description
			// 
			this.description.Dock = System.Windows.Forms.DockStyle.Fill;
			this.description.Location = new System.Drawing.Point(0, 0);
			this.description.Multiline = true;
			this.description.Name = "description";
			this.description.ReadOnly = true;
			this.description.Size = new System.Drawing.Size(552, 277);
			this.description.TabIndex = 0;
			// 
			// HttpHeaderForm
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
			this.ClientSize = new System.Drawing.Size(552, 277);
			this.Controls.Add(this.description);
			this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.SizableToolWindow;
			this.KeyPreview = true;
			this.Name = "HttpHeaderForm";
			this.Text = "HTTP Headers";
			this.ResumeLayout(false);
			this.PerformLayout();
		}
		#endregion
	}
}