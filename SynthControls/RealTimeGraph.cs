using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace SynthControls
{
    public partial class RealTimeGraph : UserControl
    {
        //
        private bool suppressRedraw = false;
        private Color bgColor = Color.Black;
        private Pen lineColor = Pens.Red;
        private float[] buffer = new float[128];
        private int write_i = 0;
        //
        private void Redraw()
        {
            if(suppressRedraw == false)
                this.Invalidate();
        }
        private void ResizeBuffer(int newSize)
        {
            float[] newBuff = new float[newSize];
            float rfactor = buffer.Length / (float)newSize;
            for(int x = 0; x < newBuff.Length; x++)
                newBuff[x] = buffer[(int)(x * rfactor)];
            buffer = newBuff;
            write_i = (int)(write_i / rfactor);
        }
        //
        public bool ManualRedraw
        {
            get { return suppressRedraw; }
            set { suppressRedraw = value; }
        }
        public int SampleSize
        {
            get { return buffer.Length; }
            set { if (value > 0 && value != buffer.Length) { ResizeBuffer(value); Redraw(); } }
        }
        public override Color BackColor
        {
            get { return bgColor; }
            set { bgColor = value; Redraw(); }
        }
        //
        public void RedrawControl()
        {
            this.Invalidate();
        }
        public void AddSample(float sample)
        {
            buffer[write_i] = sample;
            write_i++;
            if (write_i == buffer.Length)
                write_i = 0;
            Redraw();
        }
        public RealTimeGraph()
        {
            InitializeComponent();
        }
        private void RealTimeGraph_Paint(object sender, PaintEventArgs e)
        {
            e.Graphics.Clear(bgColor);
            e.Graphics.DrawLine(Pens.Purple, 0, this.Height / 2, this.Width, this.Height / 2);
            int index = (write_i) % buffer.Length;
            float widthBegin = 0;
            float widthAC = this.Width / (float)buffer.Length;
            PointF[] ps = new PointF[buffer.Length];
            for (int x = 0; x < ps.Length; x++)
            {
                ps[x].X = widthBegin;
                ps[x].Y = (1f-((buffer[index] + 1f) / 2f)) * this.Height;
                widthBegin += widthAC;
                index++; 
                if (index == buffer.Length) index = 0;
            }
            e.Graphics.DrawLines(lineColor, ps);
        }
    }
}
