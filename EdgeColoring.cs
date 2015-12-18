using System;
using System.Globalization;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;
using Visualization;

namespace EdgeColoring
{
	class EdgeColoring : Form
	{	
		GLControl glControl;
		Timer timer;
		bool idle;
		double angle;
		double xMovement;
		double yMovement;
		List<ColorPoint3D> points;
		List<List<List<List<ColorPoint3D>>>> grid;
		private float maxX = float.MinValue;
		private float minX = float.MaxValue;
		private float maxY = float.MinValue;
		private float minY = float.MaxValue;
		private float maxZ = float.MinValue;
		private float minZ = float.MaxValue;
		private int[,] directions = new int[,] {
			{-1, -1, -1}, {1, 1, 1}, {-1, -1, 0}, {1, 1, 0}, {-1, -1, 1}, {1, 1, -1}, 
			{-1, 0, -1}, {1, 0, 1}, {-1, 0, 0}, {1, 0, 0}, {-1, 0, 1}, {1, 0, -1},
			{-1, 1, -1}, {1, -1, 1}, {-1, 1, 0}, {1, -1, 0}, {-1, 1, 1}, {1, -1, -1},
			{0, -1, -1}, {0, 1, 1}, {0, -1, 0}, {0, 1, 0}, {0, -1, 1}, {0, 1, -1}, 
			{0, 0, -1}, {0, 0, 1}		
		};
		private int[,] directions2 = new int[,] {
			{-1, 0, 0}, {1, 0, 0}, {0, -1, 0}, {0, 1, 0}, {0, 0, -1}, {0, 0, 1}		
		};

		public EdgeColoring (int width, int height, string fileName)
		{
			this.Text = "Visualisation";
			this.Width = width;
			this.Height = height;

			//vytvorenie kontrolky
			glControl = new GLControl ();
			glControl.Location = new Point (10, 10);
			glControl.Size = new Size (95 * width / 100, 95 * height / 100);
			Controls.Add (glControl);

			//pridanie eventov
			glControl.Load += glControl_Load;
			glControl.Paint += glControl_Paint;
			glControl.KeyDown += glControl_KeyDown; //reakcia na stlacanie klavesov

			//automaticke prekreslenie pomocou timera (25FPS)
			timer = new Timer ();
			timer.Interval = 40;
			timer.Tick += timer_onTick;

			//existuje event, ze ak formular nic nerobi, tak generuje event, ze nic nerobi :D vie sa to zijst
			Application.Idle += (sender, e) => idle = true;

			points = readInputFromFile (fileName);
			Console.WriteLine ("Input has been successfully read.");
			points = VoxelGridFilter (0.02f);
			Console.WriteLine ("The VoxelGridAlgorithm finished.");
			points = colourByNeighbours ();
			Console.WriteLine ("Coloring finished.");
		}
		//automaticke prekreslenie
		void timer_onTick (object sender, EventArgs e)
		{
			glControl_Paint (null, null);
		}
		//priprava gl kontrolky
		void glControl_Load (object sender, EventArgs e)
		{
			GL.ClearColor (Color.Black);
			GL.Viewport (0, 0, this.Width, this.Height);
			GL.MatrixMode (MatrixMode.Projection);
			GL.LoadIdentity ();
			Matrix4 perspective = Matrix4.CreatePerspectiveFieldOfView (MathHelper.PiOver4, this.Width / this.Height, 0.1f, 100.0f);
			GL.LoadMatrix (ref perspective);
			GL.MatrixMode (MatrixMode.Modelview);
			GL.LoadIdentity ();

			angle = 180;
			yMovement = 3.5;

			timer.Start ();
		}
		//prekreslenie kontrolky
		void glControl_Paint (object sender, PaintEventArgs e)
		{
			if (!idle)
				return;

			idle = false;
			GL.Clear (ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
			draw ();
			glControl.SwapBuffers ();
		}
		//ovladanie klavesami
		void glControl_KeyDown (object sender, KeyEventArgs e)
		{	
			if (e.KeyData == Keys.W) {
				xMovement += -0.1 * Math.Sin (angle / 180 * Math.PI);
				yMovement += 0.1 * Math.Cos (angle / 180 * Math.PI);
			}
			if (e.KeyData == Keys.S) {
				xMovement += 0.1 * Math.Sin (angle / 180 * Math.PI);
				yMovement += -0.1 * Math.Cos (angle / 180 * Math.PI);
			}
			if (e.KeyData == Keys.A) {
				angle -= 0.5;
				angle %= 360;
			}
			if (e.KeyData == Keys.D) {
				angle += 0.5;
				angle %= 360;
			}
		}
		//metoda kreslenia
		void draw ()
		{
			// rotacia a posun
			GL.LoadIdentity ();
			GL.Rotate (angle, 0, 1, 0);
			GL.Translate (xMovement, 0, yMovement);

			// kreslenie bodov
			GL.PointSize (3);
			GL.Begin (PrimitiveType.Points);			

			int density = 1;
			for (int i = 0; i < points.Count; i += density) {
				Color c = Color.FromArgb (points [i].getR, points [i].getG, points [i].getB); //points nazvem nejaky list a obsahuje datovu strukturu, ktora udrziava X, Y, Z, R, G, B
				GL.Color3 (c);
				GL.Vertex3 (points [i].getX, points [i].getY, points [i].getZ);
			}
			GL.End ();
		}

		private List<ColorPoint3D> readInputFromFile (string fileName)
		{
			string[] rows = System.IO.File.ReadAllLines (fileName);
			string row = rows [1];
			string[] tokens = row.Split (new string[] { " " }, StringSplitOptions.RemoveEmptyEntries);
			int numberOfInputPoints = Int32.Parse (tokens [0]);
			List<ColorPoint3D> input = new List<ColorPoint3D> (numberOfInputPoints);

			CultureInfo ci = CultureInfo.InvariantCulture;
			//CultureInfo ci = CultureInfo.CurrentCulture;

			for (int i = 0; i < numberOfInputPoints; i++) {
				row = rows [i + 2];
				tokens = row.Split (new string[] { " " }, StringSplitOptions.RemoveEmptyEntries);
				float x = float.Parse (tokens [0], ci);
				float y = float.Parse (tokens [1], ci);
				float z = float.Parse (tokens [2], ci);
				int r = (int)(float.Parse (tokens [3], ci) * 255);
				int g = (int)(float.Parse (tokens [4], ci) * 255);
				int b = (int)(float.Parse (tokens [5], ci) * 255);
				ColorPoint3D point = new ColorPoint3D (x, y, z, r, g, b);
				input.Add (point);
			}

			return input;
		}

		private void getRange ()
		{
			foreach (ColorPoint3D point in points) {
				if (point.getX > maxX) {
					maxX = point.getX;
				} else {
					if (point.getX < minX) {
						minX = point.getX;
					}
				}

				if (point.getY > maxY) {
					maxY = point.getY;
				} else {
					if (point.getY < minY) {
						minY = point.getY;
					}
				}

				if (point.getZ > maxZ) {
					maxZ = point.getZ;
				} else {
					if (point.getZ < minZ) {
						minZ = point.getZ;
					}
				}
			}
		}

		private void dividePoints (float voxelSize)
		{
			int xCount = (int)Math.Ceiling ((maxX - minX) / voxelSize);
			int yCount = (int)Math.Ceiling ((maxY - minY) / voxelSize);
			int zCount = (int)Math.Ceiling ((maxZ - minZ) / voxelSize);
			grid = new List<List<List<List<ColorPoint3D>>>> (xCount);

			// vytvorenie 4-rozmerného poľa
			for (int i = 0; i < xCount; i ++) {
				List<List<List<ColorPoint3D>>> list = new List<List<List<ColorPoint3D>>> (yCount);
				grid.Add (list);
				for (int j = 0; j < yCount; j++) {
					List<List<ColorPoint3D>> list2 = new List<List<ColorPoint3D>> (zCount);
					list.Add (list2);
					for (int k = 0; k < zCount; k++) {
						List<ColorPoint3D> list3 = new List<ColorPoint3D> ();
						list2.Add (list3);
					}
				}
			}

			// samotné zadelenie bodov
			foreach (ColorPoint3D point in points) {
				float x = point.getX;
				float y = point.getY;
				float z = point.getZ;
				int xID = (int)Math.Floor ((x - minX) / voxelSize);
				int yID = (int)Math.Floor ((y - minY) / voxelSize);
				int zID = (int)Math.Floor ((z - minZ) / voxelSize);
				grid [xID] [yID] [zID].Add (point);
			}
		}

		private ColorPoint3D calculateCenter (List<ColorPoint3D> points)
		{
			float sumX = 0;
			float sumY = 0;
			float sumZ = 0;
			int sumR = 0;
			int sumG = 0;
			int sumB = 0;
			int count = points.Count;

			foreach (ColorPoint3D point in points) {
				sumX += point.getX;
				sumY += point.getY;
				sumZ += point.getZ;
				sumR += point.getR;
				sumG += point.getG;
				sumB += point.getB;
			}

			return new ColorPoint3D (sumX / count, sumY / count, sumZ / count, sumR / count, sumG / count, sumB / count);
		}

		private List<ColorPoint3D> VoxelGridFilter (float voxelSize)
		{
			List<ColorPoint3D> output = new List<ColorPoint3D> (points.Count);
			getRange ();
			dividePoints (voxelSize);
			for (int i = 0; i < grid.Count; i++) {
				for (int j = 0; j < grid[i].Count; j++) {
					for (int k = 0; k < grid[i][j].Count; k++) {
						if (grid [i] [j] [k].Count > 0) {
							if (grid [i] [j] [k].Count == 1) {
								output.Add (grid [i] [j] [k] [0]);
							} else {
								ColorPoint3D center = calculateCenter (grid [i] [j] [k]);
								output.Add (center);
								grid [i] [j] [k] = new List<ColorPoint3D> (1);
								grid [i] [j] [k].Add (center);
							}
						}
					}
				}
			}		
			return output;
		}

		private List<ColorPoint3D> colourByNeighbours ()
		{
			List<ColorPoint3D> output = new List<ColorPoint3D> (points.Count);

			for (int i = 0; i < grid.Count; i++) {
				for (int j = 0; j < grid[i].Count; j++) {
					for (int k = 0; k < grid[i][j].Count; k++) {
						if (grid [i] [j] [k].Count > 0) {
							ColorPoint3D originalPoint = grid [i] [j] [k] [0];

							int xChange = 0;
							int yChange = 0;
							int zChange = 0;

							for (int l = 0; l < directions.GetLength(0); l++) {
								if (i + directions [l, 0] >= 0 && i + directions [l, 0] < grid.Count &&
									j + directions [l, 1] >= 0 && j + directions [l, 1] < grid [i].Count &&
									k + directions [l, 2] >= 0 && k + directions [l, 2] < grid [i] [j].Count &&
									grid [i + directions [l, 0]] [j + directions [l, 1]] [k + directions [l, 2]].Count > 0) {
									xChange += directions [l, 0];
									yChange += directions [l, 1];
									zChange += directions [l, 2];
								}
							}
							int change = Math.Abs (xChange) + Math.Abs (yChange) + Math.Abs (zChange);
							//Console.WriteLine (change + " " + xChange + " " + yChange + " " + zChange);

							int colorConstant = 255 / 15 * change;
							//if (Math.Abs (change) < 9) {
							//	colorConstant = 0;
							//}
							ColorPoint3D newPoint = new ColorPoint3D (originalPoint.getX, originalPoint.getY, originalPoint.getZ, colorConstant, colorConstant, colorConstant);
							output.Add (newPoint);
						}
					}
				}
			}

			return output;
		}

		public static void Main ()
		{
			try {
				EdgeColoring edgeColoring = new EdgeColoring (1800, 900, "../../pc0.off");			
				edgeColoring.ShowDialog ();
			} catch (Exception e) {
				Console.Write (e.StackTrace);
			}
		}
	}
}