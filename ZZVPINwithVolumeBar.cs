#region Using declarations
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Xml.Serialization;
using NinjaTrader.Cbi;
using NinjaTrader.Data;
using NinjaTrader.Gui.Chart;
#endregion

// This namespace holds all indicators and is required. Do not change it.
namespace NinjaTrader.Indicator
{
    /// <summary>
    /// Implements volume bucketing ala Marcos
    /// </summary>
    [Description("Implements volume bucketing ala Marcos")]
    public class ZZVPINwithVolumeBar : Indicator
    {
        #region Variables
            private int volumeBarSize = 8000; // Default setting for VolumeBarSize
			private int jmaLen = 20;
			private double sigmaDeltaP = 0;   // stddev of price change
			private double deltaP = 0;			// delta P
			private DataSeries	deltaPrice;
			private DataSeries	deltaPriceOverSigmal;// (deltaP/sigmaDeltaP) dataseries
			private DataSeries	vb;	
			private DataSeries 	vs;
			private DataSeries  vb_bucket;	// store the summation of last 3 vb (vol=8000, bucket=24000)
			private DataSeries	vs_bucket;
			private DataSeries 	VPIN;
			private int n_windowsize = 50;
			private int useCDF = 1;
			private DataSeries cdfVPIN;
		#endregion

        /// <summary>
        /// This method is used to configure the indicator and is called once before any bar data is loaded.
        /// </summary>
        protected override void Initialize()
        {
            Add(new Plot(Color.FromKnownColor(KnownColor.Black), PlotStyle.Line, "VolBucketPlot"));
            Overlay				= false;
			deltaPrice	= new DataSeries(this, MaximumBarsLookBack.Infinite);
			deltaPriceOverSigmal = new DataSeries(this, MaximumBarsLookBack.Infinite);
			vb = new DataSeries(this, MaximumBarsLookBack.Infinite);
			vs = new DataSeries(this, MaximumBarsLookBack.Infinite);
			vb_bucket = new DataSeries(this, MaximumBarsLookBack.Infinite);
			vs_bucket = new DataSeries(this, MaximumBarsLookBack.Infinite);
			VPIN = new DataSeries(this, MaximumBarsLookBack.Infinite);
			cdfVPIN = new DataSeries(this, MaximumBarsLookBack.Infinite);
        }

        /// <summary>
        /// Called on each bar update event (incoming tick)
        /// </summary>
        protected override void OnBarUpdate()
        {
			double normdistvalue=0;
			double vb_bucket_total;
			double vs_bucket_total;
			double accum_vdelta = 0;
			double sigmaVPIN = 0;
			
			if (CurrentBar < 4) 
			{
				return;
			}
			
			deltaPrice[0] = Close[0] - Close[1];
			//Print( "deltaPrice = " + deltaPrice[0] );
			
			if (CurrentBar < 100) 
			{
				return;
			}
			
			// Formula for bulk volume classification is
			// Vb = V * CDF(deltaP/sigmadeltaP)
			//
			sigmaDeltaP =  StdDev(deltaPrice,30)[0];
			deltaPriceOverSigmal[0] = deltaPrice[0] / sigmaDeltaP;
			normdistvalue = NORMDIST( deltaPriceOverSigmal[0], /*SMA(deltaPriceOverSigmal,200)[0]*/0 , StdDev(deltaPriceOverSigmal,100)[0], true);
			vb[0] = volumeBarSize * normdistvalue;
			vs[0] = volumeBarSize - vb[0];
			//Print(" normdistvalue = " + normdistvalue);
			//Print(" vb = " + vb[0]);
			//Print("bar = " + CurrentBar); 
			//Print("time = " + Time[0].Date + "  " + "  " + Time[0].Hour + "  " + Time[0].Minute); 
			
			// Sum Vb 3 times to make a vol bucket of V = 24000
			if(CurrentBar>103){
				vb_bucket[0] = vb[0] + vb[1] + vb[2];
				vs_bucket[0] = vs[0] + vs[1] + vs[2];
			}
			// update VPIN calculation
			// Sum of ( |vs_bucket - vb_bucket| ) over n / n.V
			//
			if( CurrentBar > 150 )
			{
				for (int i = 0; i <n_windowsize; i++)
        		{
            		accum_vdelta  += Math.Abs(vs_bucket[i] - vb_bucket[i]);	
				}
				VPIN[0] = accum_vdelta / ( n_windowsize * 24000 );
			
				// Calculate CDF of VPIN
				sigmaVPIN =  StdDev(VPIN,100)[0];
				cdfVPIN[0] = NORMDIST( VPIN[0], SMA(VPIN,200)[0] , sigmaVPIN, true);
				 
			
			
			}
				
			
					
			if(  Double.IsNaN(vb[0]) ){
				VolBucketPlot.Set(0);
			}
			else
			{
				if(useCDF > 0)
					//VolBucketPlot.Set(cdfVPIN[0]);
					VolBucketPlot.Set(JurikJMA(cdfVPIN,50,10)[0]);
				else
				   	VolBucketPlot.Set(VPIN[0]);
			}
			
			// Sum vb over volume bucket size
			// if vol bucket size is 24000, then you add 3 consecutive vb
	
			// or, set chart to 24000 vol
			// count if new bar, then count until 8000, calculate vb
        }

        #region Properties
        [Browsable(false)]	// this line prevents the data series from being displayed in the indicator properties dialog, do not remove
        [XmlIgnore()]		// this line ensures that the indicator can be saved/recovered as part of a chart template, do not remove
        public DataSeries VolBucketPlot
        {
            get { return Values[0]; }
        }

        [Description("Default for ES is 8000")]
        [GridCategory("Parameters")]
        public int VolumeBarSize
        {
            get { return volumeBarSize; }
            set { volumeBarSize = Math.Max(1, value); }
        }
        [Description("jma length")]
        [GridCategory("Parameters")]
        public int JmaLen
        {
            get { return jmaLen; }
            set { jmaLen = Math.Max(1, value); }
        }

	    [Description("n window length")]
        [GridCategory("Parameters")]
        public int N_windowsize
        {
            get { return n_windowsize; }
            set { n_windowsize = Math.Max(1, value); }
        }
	    [Description("user CDF")]
        [GridCategory("Parameters")]
        public int UseCDF
        {
            get { return useCDF; }
            set { useCDF = value; }
        }	
		
		
        #endregion	
		
	#region Miscellaneous
/// <summary>The normdist.</summary>
        /// <param name="x">The x.</param>
        /// <param name="mean">The mean.</param>
        /// <param name="std">The std. </param>
        /// <param name="cumulative">The cumulative.</param>
        /// <returns>The normdist.</returns>
        private static double NORMDIST(double x, double mean, double std, bool cumulative)
        {
            if (cumulative)
            {
                return Phi(x, mean, std);
            }

            var tmp = 1/(Math.Sqrt(2*Math.PI)*std);
            return tmp*Math.Exp(-.5*Math.Pow((x - mean)/std, 2));
        }

        // from http://www.cs.princeton.edu/introcs/...Math.java.html
        // fractional error less than 1.2 * 10 ^ -7.

        // cumulative normal distribution
        /// <summary>The phi.</summary>
        /// <param name="z">The z.</param>
        /// <returns>The phi.</returns>
        private static double Phi(double z)
        {
            return 0.5*(1.0 + erf(z/Math.Sqrt(2.0)));
        }

        // cumulative normal distribution with mean mu and std deviation sigma
        /// <summary>The phi.</summary>
        /// <param name="z">The z.</param>
        /// <param name="mu">The mu.</param>
        /// <param name="sigma">The sigma.</param>
        /// <returns>The phi.</returns>
        private static double Phi(double z, double mu, double sigma)
        {
            return Phi((z - mu)/sigma);
        }

        /// <summary>The erf.</summary>
        /// <param name="z">The z.</param>
        /// <returns>The erf.</returns>
        private static double erf(double z)
        {
            var t = 1.0/(1.0 + 0.5*Math.Abs(z));

            // use Horner's method
            var ans = 1 -
                      t*
                      Math.Exp(
                          -z*z - 1.26551223 +
                          t*
                          (1.00002368 +
                           t*
                           (0.37409196 +
                            t*
                            (0.09678418 +
                             t*
                             (-0.18628806 +
                              t*
                              (0.27886807 +
                               t*(-1.13520398 + t*(1.48851587 + t*(-0.82215223 + t*0.17087277)))))))));
            if (z >= 0)
            {
                return ans;
            }

            return -ans;
        }
		
	#endregion
    }
}

#region NinjaScript generated code. Neither change nor remove.
// This namespace holds all indicators and is required. Do not change it.
namespace NinjaTrader.Indicator
{
    public partial class Indicator : IndicatorBase
    {
        private ZZVPINwithVolumeBar[] cacheZZVPINwithVolumeBar = null;

        private static ZZVPINwithVolumeBar checkZZVPINwithVolumeBar = new ZZVPINwithVolumeBar();

        /// <summary>
        /// Implements volume bucketing ala Marcos
        /// </summary>
        /// <returns></returns>
        public ZZVPINwithVolumeBar ZZVPINwithVolumeBar(int jmaLen, int n_windowsize, int useCDF, int volumeBarSize)
        {
            return ZZVPINwithVolumeBar(Input, jmaLen, n_windowsize, useCDF, volumeBarSize);
        }

        /// <summary>
        /// Implements volume bucketing ala Marcos
        /// </summary>
        /// <returns></returns>
        public ZZVPINwithVolumeBar ZZVPINwithVolumeBar(Data.IDataSeries input, int jmaLen, int n_windowsize, int useCDF, int volumeBarSize)
        {
            if (cacheZZVPINwithVolumeBar != null)
                for (int idx = 0; idx < cacheZZVPINwithVolumeBar.Length; idx++)
                    if (cacheZZVPINwithVolumeBar[idx].JmaLen == jmaLen && cacheZZVPINwithVolumeBar[idx].N_windowsize == n_windowsize && cacheZZVPINwithVolumeBar[idx].UseCDF == useCDF && cacheZZVPINwithVolumeBar[idx].VolumeBarSize == volumeBarSize && cacheZZVPINwithVolumeBar[idx].EqualsInput(input))
                        return cacheZZVPINwithVolumeBar[idx];

            lock (checkZZVPINwithVolumeBar)
            {
                checkZZVPINwithVolumeBar.JmaLen = jmaLen;
                jmaLen = checkZZVPINwithVolumeBar.JmaLen;
                checkZZVPINwithVolumeBar.N_windowsize = n_windowsize;
                n_windowsize = checkZZVPINwithVolumeBar.N_windowsize;
                checkZZVPINwithVolumeBar.UseCDF = useCDF;
                useCDF = checkZZVPINwithVolumeBar.UseCDF;
                checkZZVPINwithVolumeBar.VolumeBarSize = volumeBarSize;
                volumeBarSize = checkZZVPINwithVolumeBar.VolumeBarSize;

                if (cacheZZVPINwithVolumeBar != null)
                    for (int idx = 0; idx < cacheZZVPINwithVolumeBar.Length; idx++)
                        if (cacheZZVPINwithVolumeBar[idx].JmaLen == jmaLen && cacheZZVPINwithVolumeBar[idx].N_windowsize == n_windowsize && cacheZZVPINwithVolumeBar[idx].UseCDF == useCDF && cacheZZVPINwithVolumeBar[idx].VolumeBarSize == volumeBarSize && cacheZZVPINwithVolumeBar[idx].EqualsInput(input))
                            return cacheZZVPINwithVolumeBar[idx];

                ZZVPINwithVolumeBar indicator = new ZZVPINwithVolumeBar();
                indicator.BarsRequired = BarsRequired;
                indicator.CalculateOnBarClose = CalculateOnBarClose;
#if NT7
                indicator.ForceMaximumBarsLookBack256 = ForceMaximumBarsLookBack256;
                indicator.MaximumBarsLookBack = MaximumBarsLookBack;
#endif
                indicator.Input = input;
                indicator.JmaLen = jmaLen;
                indicator.N_windowsize = n_windowsize;
                indicator.UseCDF = useCDF;
                indicator.VolumeBarSize = volumeBarSize;
                Indicators.Add(indicator);
                indicator.SetUp();

                ZZVPINwithVolumeBar[] tmp = new ZZVPINwithVolumeBar[cacheZZVPINwithVolumeBar == null ? 1 : cacheZZVPINwithVolumeBar.Length + 1];
                if (cacheZZVPINwithVolumeBar != null)
                    cacheZZVPINwithVolumeBar.CopyTo(tmp, 0);
                tmp[tmp.Length - 1] = indicator;
                cacheZZVPINwithVolumeBar = tmp;
                return indicator;
            }
        }
    }
}

// This namespace holds all market analyzer column definitions and is required. Do not change it.
namespace NinjaTrader.MarketAnalyzer
{
    public partial class Column : ColumnBase
    {
        /// <summary>
        /// Implements volume bucketing ala Marcos
        /// </summary>
        /// <returns></returns>
        [Gui.Design.WizardCondition("Indicator")]
        public Indicator.ZZVPINwithVolumeBar ZZVPINwithVolumeBar(int jmaLen, int n_windowsize, int useCDF, int volumeBarSize)
        {
            return _indicator.ZZVPINwithVolumeBar(Input, jmaLen, n_windowsize, useCDF, volumeBarSize);
        }

        /// <summary>
        /// Implements volume bucketing ala Marcos
        /// </summary>
        /// <returns></returns>
        public Indicator.ZZVPINwithVolumeBar ZZVPINwithVolumeBar(Data.IDataSeries input, int jmaLen, int n_windowsize, int useCDF, int volumeBarSize)
        {
            return _indicator.ZZVPINwithVolumeBar(input, jmaLen, n_windowsize, useCDF, volumeBarSize);
        }
    }
}

// This namespace holds all strategies and is required. Do not change it.
namespace NinjaTrader.Strategy
{
    public partial class Strategy : StrategyBase
    {
        /// <summary>
        /// Implements volume bucketing ala Marcos
        /// </summary>
        /// <returns></returns>
        [Gui.Design.WizardCondition("Indicator")]
        public Indicator.ZZVPINwithVolumeBar ZZVPINwithVolumeBar(int jmaLen, int n_windowsize, int useCDF, int volumeBarSize)
        {
            return _indicator.ZZVPINwithVolumeBar(Input, jmaLen, n_windowsize, useCDF, volumeBarSize);
        }

        /// <summary>
        /// Implements volume bucketing ala Marcos
        /// </summary>
        /// <returns></returns>
        public Indicator.ZZVPINwithVolumeBar ZZVPINwithVolumeBar(Data.IDataSeries input, int jmaLen, int n_windowsize, int useCDF, int volumeBarSize)
        {
            if (InInitialize && input == null)
                throw new ArgumentException("You only can access an indicator with the default input/bar series from within the 'Initialize()' method");

            return _indicator.ZZVPINwithVolumeBar(input, jmaLen, n_windowsize, useCDF, volumeBarSize);
        }
    }
}
#endregion
