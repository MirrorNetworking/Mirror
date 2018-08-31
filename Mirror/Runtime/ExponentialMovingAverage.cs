using System;
namespace Mirror
{
    // implementation of N-day EMA
    // it calculates an exponential moving average roughy equivalent to the last n observations
    public class ExponentialMovingAverage
    {
        private float alpha;
        private bool initialized = false;

        private double _value = 0;
        private double _var = 0;

        public ExponentialMovingAverage(int n)
        {
            // standard N-day EMA alpha calculation
            alpha = 2.0f / (n + 1);
        }

        public void Add(double newValue)
        {
            // simple algorithm for EMA described here:
            // https://en.wikipedia.org/wiki/Moving_average#Exponentially_weighted_moving_variance_and_standard_deviation
            if (initialized)
            {
                double delta = newValue - _value;
                _value = _value + alpha * delta;
                _var = (1 - alpha) * (_var + alpha * delta * delta);
            }
            else
            {
                _value = newValue;
                initialized = true;
            }
        }

        public double Value {
            get {
                return _value;
            }
        }

        public double Var {
            get {
                return _var;
            }
        }
    }
}
