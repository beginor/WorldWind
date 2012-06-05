using System.Windows.Forms;
using WorldWind;

namespace WorldWindCTest {

	public partial class TestForm : Form {
		
		private WorldWindow _worldWindow;

		public TestForm() {
			Application.DoEvents();
			InitializeComponent();

			this.SuspendLayout();
			this._worldWindow = new WorldWindow();
			this._worldWindow.Dock = System.Windows.Forms.DockStyle.Fill;
			this._worldWindow.Name = "worldWindow1";
			this.Controls.Add(this._worldWindow);
			this.ResumeLayout();

			this._worldWindow.Render();
		}

		static void Main(string[] args) {
			Application.EnableVisualStyles();
			Application.SetCompatibleTextRenderingDefault(false);
			TestForm form = new TestForm();
			Application.Run(form);
		}
	}
}