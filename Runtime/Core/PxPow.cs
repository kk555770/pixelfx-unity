// Math.pow correctly rounded, in plain double arithmetic, so every platform draws the same pixels. The C runtimes' pows
// (Windows' ucrtbase, macOS's, Mono's) round about one input in a thousand differently, enough to move a pixel across a
// dither threshold; Tools/pow.mjs gives the JS original this same pow when the parity data is made.
// The method: ln x and then e^(y ln x) in double-double (about 100 bits) with 128-entry tables, rounded once at the end,
// after a quicker try in mostly single doubles that settles all but about one x^y in a thousand.
using System;
using System.Runtime.CompilerServices;

namespace Pixelfx
{
    public static partial class Px
    {
        /// <summary>
        /// Math.pow with V8's own answers for a NaN exponent, 1 or -1 to an infinite power, squares and square roots, and
        /// otherwise x^y rounded to the nearest double.
        /// </summary>
        public static double Pow(double x, double y)
        {
            if (double.IsNaN(y)) return double.NaN;
            if (double.IsInfinity(y) && (x == 1 || x == -1)) return double.NaN;
            if (y == 2) return x * x;
            if (y == 0.5) return double.IsInfinity(x) ? double.PositiveInfinity : Math.Sqrt(x + 0);
            if (x < 0 && !double.IsInfinity(x) && !double.IsInfinity(y) && y == Math.Floor(y))
                return y % 2 != 0 ? -RoundedPow(-x, y) : RoundedPow(-x, y);
            return RoundedPow(x, y);
        }

        static double RoundedPow(double x, double y)
        {
            // zero, infinities, NaN, negative bases, x^0 and 1^y have exact answers that every pow agrees on
            if (!(x > 0) || double.IsInfinity(x) || double.IsInfinity(y) || y == 0 || x == 1) return Math.Pow(x, y);
            if (QuickPow(x, y, out double quick)) return quick;

            Ln(x, out double lh, out double ll);
            double estimate = y * lh;
            if (estimate > 710) return double.PositiveInfinity;
            if (estimate < -746) return 0; // below half the smallest subnormal
            TwoProd(y, lh, out double zh, out double zl);
            FastTwoSum(zh, zl + y * ll, out zh, out zl);
            return Exp(zh, zl);
        }

        // The same steps with single doubles wherever the term is small enough, to about 2^-68 of x^y: if x^y +- 2^-63 of it
        // round to the same double, that is the answer. About one x^y in a thousand lies closer to a halfway point and
        // goes the double-double way. Takes normal x, |y| <= 64 and results well inside the normal range.
        static bool QuickPow(double x, double y, out double result)
        {
            result = 0;
            long bits = BitConverter.DoubleToInt64Bits(x);
            int e = (int)(bits >> 52) - 1023;
            if (e == -1023 || Math.Abs(y) > 64) return false;
            double m = BitConverter.Int64BitsToDouble((bits & 0x000FFFFFFFFFFFFFL) | 0x3FF0000000000000L);
            int i = (int)((bits >> 45) & 127);
            double c = 1 + (i + 0.5) / 128;

            // u = uh + ul; 2 atanh(u) = 2u + tail
            TwoSum(m, c, out double dh, out double dl);
            double num = m - c, uh = num / dh;
            TwoProd(uh, dh, out double ph, out double pl);
            double ul = (num - ph - pl - uh * dl) / dh, t = uh * uh;
            double tail = uh * t * (2.0 / 3 + t * (0.4 + t * (2.0 / 7 + t * (2.0 / 9))));

            // ln x = e ln2 + ln c + 2u + tail, with e Ln2Short exact
            TwoSum(e * Ln2Short, LnC[2 * i], out double s, out double se);
            TwoSum(s, 2 * uh, out s, out double se2);
            TwoSum(s, se + se2 + (e * Ln2Rest + LnC[2 * i + 1] + 2 * ul + tail), out double lh, out double ll);

            TwoProd(y, lh, out double zh, out double zl);
            zl += y * ll;
            if (!(zh > -707 && zh < 709)) return false;

            // e^z = 2^k 2^(j/128) (1 + rh + rl + rh rl + p), with n Ln2Over128Short exact and zh - n Ln2Over128Short exact
            double n = Math.Round(zh * InvLn2Over128);
            int ni = (int)n, k = ni >> 7, j = ni & 127;
            TwoSum(zh - n * Ln2Over128Short, zl - n * Ln2Over128Rest, out double rh, out double rl);
            double p =rh * rh * (0.5 + rh * (1.0 / 6 + rh * (1.0 / 24 + rh * (1.0 / 120 + rh * (1.0 / 720 + rh * (1.0 / 5040))))));
            double th = Exp2J[2 * j], tl = Exp2J[2 * j + 1];
            TwoProd(th, rh, out double qh, out double ql);
            FastTwoSum(th, qh, out double hi, out double lo);
            FastTwoSum(hi, lo + ql + th * (rl + rh * rl + p) + tl * (1 + rh), out hi, out lo);

            double err = hi * 1.0842021724855044e-19; // 2^-63
            double up = hi + (lo + err), down = hi + (lo - err);
            if (up != down) return false;
            result = up * PowerOfTwo(k);
            return true;
        }

        // ln x = e ln 2 + ln c + 2 atanh(u), with c the centre of x's 1/128 slice of [1, 2) and u = (m - c) / (m + c), |u| <= 2^-9
        static void Ln(double x, out double hi, out double lo)
        {
            long bits = BitConverter.DoubleToInt64Bits(x);
            int e = (int)(bits >> 52) - 1023;
            if (e == -1023)
            {
                bits = BitConverter.DoubleToInt64Bits(x * 18014398509481984.0); // 2^54
                e = (int)(bits >> 52) - 1023 - 54;
            }
            double m = BitConverter.Int64BitsToDouble((bits & 0x000FFFFFFFFFFFFFL) | 0x3FF0000000000000L);
            int i = (int)((bits >> 45) & 127);
            double c = 1 + (i + 0.5) / 128;

            TwoSum(m, c, out double dh, out double dl);
            double num = m - c, q1 = num / dh;
            TwoProd(q1, dh, out double ph, out double pl);
            double q2 = (num - ph - pl - q1 * dl) / dh;
            FastTwoSum(q1, q2, out double uh, out double ul);

            // 2 atanh(u) = 2u + u t (2/3 + t (2/5 + t (2/7 + t (2/9 + t 2/11)))), t = u^2
            Mul(uh, ul, uh, ul, out double th, out double tl);
            double s = 0.4 + th * (2.0 / 7 + th * (2.0 / 9 + th * (2.0 / 11)));
            FastTwoSum(TwoThirdsHi, th * s, out double ih, out double il);
            il += TwoThirdsLo;
            Mul(uh, ul, th, tl, out double wh, out double wl);
            Mul(wh, wl, ih, il, out wh, out wl);
            Add(2 * uh, 2 * ul, wh, wl, out double ah, out double al);

            Add(LnC[2 * i], LnC[2 * i + 1], ah, al, out ah, out al);
            TwoProd(e, Ln2Hi, out double eh, out double el);
            Add(eh, el + e * Ln2Lo, ah, al, out hi, out lo);
        }

        // e^z = 2^k 2^(j/128) e^r, z = (128k + j) ln2/128 + r, |r| <= ln2/256
        static double Exp(double zh, double zl)
        {
            double n = Math.Round(zh * InvLn2Over128);
            int ni = (int)n, k = ni >> 7, j = ni & 127;
            TwoProd(n, Ln2Over128Hi, out double ph, out double pl);
            FastTwoSum(zh - ph, zl - pl - n * Ln2Over128Lo, out double rh, out double rl);

            // e^r - 1 = r + r^2 (1/2 + r (1/6 + r (1/24 + r q))), q = 1/120 + r/720 + ... in double
            double q = 1.0 / 120 + rh * (1.0 / 720 + rh * (1.0 / 5040 + rh * (1.0 / 40320 + rh * (1.0 / 362880))));
            FastTwoSum(InvTwentyFourHi, rh * q, out double ch, out double cl);
            cl += InvTwentyFourLo;
            Mul(rh, rl, ch, cl, out ch, out cl);
            Add(InvSixHi, InvSixLo, ch, cl, out ch, out cl);
            Mul(rh, rl, ch, cl, out ch, out cl);
            Add(0.5, 0, ch, cl, out ch, out cl);
            Mul(rh, rl, rh, rl, out double sh, out double sl);
            Mul(sh, sl, ch, cl, out ch, out cl);
            Add(rh, rl, ch, cl, out double mh, out double ml);

            double th = Exp2J[2 * j], tl = Exp2J[2 * j + 1];
            Mul(th, tl, mh, ml, out mh, out ml);
            Add(th, tl, mh, ml, out mh, out ml);
            double result = mh + ml;
            if (k > 1023) return result * 2 * PowerOfTwo(k - 1);
            if (k > -1022 || (k == -1022 && result >= 1)) return result * PowerOfTwo(k);

            // a subnormal: round (mh + ml) 2^(k + 1074) to an integer once, halves to even, then scale it by 2^-1074 exactly
            double scale = PowerOfTwo(k + 1074), a = mh * scale, units = Math.Round(a), f = (a - units) + ml * scale;
            if (f > 0.5 || (f == 0.5 && units % 2 != 0)) units++;
            else if (f < -0.5 || (f == -0.5 && units % 2 != 0)) units--;
            return units * double.Epsilon;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static double PowerOfTwo(int k) => BitConverter.Int64BitsToDouble((long)(k + 1023) << 52);

        // ---- double-double arithmetic: a value is hi + lo with |lo| <= half an ulp of hi

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void TwoSum(double a, double b, out double s, out double e)
        {
            s = a + b;
            double bb = s - a;
            e = (a - (s - bb)) + (b - bb);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void FastTwoSum(double a, double b, out double s, out double e)
        {
            s = a + b;
            e = b - (s - a);
        }

        // Dekker's product: Unity's Mono has no Math.FusedMultiplyAdd
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void TwoProd(double a, double b, out double p, out double e)
        {
            p = a * b;
            double ta = 134217729.0 * a, ah = ta - (ta - a), al = a - ah;
            double tb = 134217729.0 * b, bh = tb - (tb - b), bl = b - bh;
            e = ((ah * bh - p) + ah * bl + al * bh) + al * bl;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void Add(double ah, double al, double bh, double bl, out double hi, out double lo)
        {
            TwoSum(ah, bh, out double s, out double e);
            TwoSum(al, bl, out double t, out double f);
            FastTwoSum(s, e + t, out s, out e);
            FastTwoSum(s, e + f, out hi, out lo);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void Mul(double ah, double al, double bh, double bl, out double hi, out double lo)
        {
            TwoProd(ah, bh, out double p, out double e);
            FastTwoSum(p, e + (ah * bl + al * bh), out hi, out lo);
        }

        const double Ln2Hi = 0.6931471805599453, Ln2Lo = 2.3190468138462996e-17;
        // ln 2 and ln 2 / 128 cut to 42 and 36 significant bits, so a product with an exponent or with n is exact, and the rest
        const double Ln2Short = 0.6931471805598903, Ln2Rest = 5.497923018708371e-14;
        const double Ln2Over128Short = 0.005415212348111709, Ln2Over128Rest = 1.2864023111638346e-14;
        const double Ln2Over128Hi =0.0054152123481245725, Ln2Over128Lo = 1.8117553233174215e-19, InvLn2Over128 = 184.6649652337873;
        const double TwoThirdsHi = 0.6666666666666666, TwoThirdsLo = 3.700743415417188e-17;
        const double InvSixHi = 0.16666666666666666, InvSixLo = 9.25185853854297e-18;
        const double InvTwentyFourHi = 0.041666666666666664, InvTwentyFourLo = 2.3129646346357427e-18;

        // ln(1 + (i + 1/2) / 128) for i = 0..127, as hi, lo pairs
        static readonly double[] LnC =
        {
            0.003898640415657323, 1.2541659038304973e-19, 0.011650617219975274, -2.3618788515509035e-19,
            0.019342962843130935, -2.2760589303784623e-19, 0.026976587698202076, -5.651841481310676e-20,
            0.034552381506659735, -1.6591063781278726e-18, 0.04207121392068706, -3.1329038365070074e-18,
            0.04953393512227663, 3.3991672076404202e-18, 0.056941376400138424, 4.849020418096643e-19,
            0.06429435070539725, 2.607864228825769e-18, 0.07159365318700882, -3.804421579719008e-19,
            0.07884006170777602, 3.2379150876431256e-18, 0.08603433734180316, -4.235394883227454e-18,
            0.0931772248541833, -6.707547381997404e-18, 0.10026945316367515, -1.9556371293694694e-18,
            0.10731173578908805, 4.480328406815626e-19, 0.11430477128005863, 5.1100358927720175e-18,
            0.12124924363286968, 5.284805187745387e-18, 0.12814582269193003, 4.564146029872488e-18,
            0.13499516453750482, 1.1344320488590788e-17, 0.14179791186025734, 1.3587228662372945e-17,
            0.14855469432313714, -1.53995371858771e-19, 0.15526612891112396, -5.790029056368188e-18,
            0.16193282026931324, 9.773924675229098e-18, 0.16855536102980667, -4.849378323802459e-18,
            0.17513433212784915, -3.59146702814679e-18, 0.18167030310763468, -5.8870920167715034e-18,
            0.188163832418183, -4.497983271338944e-18, 0.19461546769967167, -9.286606646402599e-18,
            0.20102574606059073, 9.307006919883831e-18, 0.2073951943460706, -6.623981508424082e-18,
            0.21372432939771813, 1.1984668242736255e-17, 0.2200136583052821, -1.0079574422441999e-17,
            0.22626367865045338, 7.90387942889578e-18, 0.23247487874309405, 1.049773658067578e-17,
            0.238647737850175, -2.480208795706813e-18, 0.24478272641769092, 7.690455270851944e-19,
            0.25088030628580943, -1.2457039343986644e-17, 0.2569409308975004, 6.30788074376329e-18,
            0.26296504550088134, 7.045250208263107e-18, 0.26895308734550394, 2.0567264884778372e-17,
            0.2749054858727992, 2.2401714494357158e-17, 0.2808226629008878, -2.4827800962650586e-17,
            0.2867050328039543, -3.679022556770764e-18, 0.29255300268637746, -2.1327310101814576e-17,
            0.2983669725517973, -1.1440869858035824e-18, 0.3041473354672967, -2.963837507561865e-18,
            0.3098944777228647, -2.619160572200562e-17, 0.31560877898630335, -1.613154981740814e-17,
            0.3212906124537343, -1.1275300634302997e-17, 0.32694034499585334, -1.7491334247872663e-17,
            0.3325583373000766, -1.0452065576244321e-17, 0.3381449440087164, -2.1615585875304225e-17,
            0.34370051385331846, -1.2044907642022741e-17, 0.34922538978528833, -2.7353198661030995e-17,
            0.35471990910292905, -2.5723845333224125e-17, 0.3601844035750078, -9.183161098421605e-18,
            0.3656191995609647, -1.3629378153461892e-17, 0.3710246181278727, -2.122406120993782e-17,
            0.3764009751642531, -1.610798090541652e-17, 0.38174858149084834, 2.1657332748577932e-18,
            0.3870677429684483, -2.605712886799137e-17, 0.3923587606028639, -2.606843002816114e-17,
            0.39762193064713847, 2.2863243298276973e-17, 0.40285754470108354, -2.1765129825682792e-17,
            0.4080658898082217, 2.168796643366152e-17, 0.4132472485502193, 4.912298436809646e-18,
            0.4184018991388838, 2.611504211531839e-18, 0.4235301155058033, 2.278340456200509e-17,
            0.4286321673896988, -2.1405327278937043e-17, 0.4337083204215594, 1.441489970357422e-17,
            0.43875883620762796, -1.8037719679045852e-17, 0.443783972410301, -5.239134183313928e-18,
            0.4487839828270067, -5.565509526284787e-19, 0.4537591174671205, 8.09899861330878e-18,
            0.4587096226269767, -1.1485607746843534e-17, 0.4636357409630325, 7.601622534719796e-18,
            0.46853771156323926, 1.3979691184669512e-17, 0.4734157700166721, 9.716435657790641e-18,
            0.47827014848147026, 2.177784944619191e-17, 0.4831010757511358, 1.1606798792464553e-17,
            0.487908777319239, -1.1058488059963112e-17, 0.49269347544257525, 9.624601235942604e-18,
            0.49745538920281895, -1.164677423484706e-17, 0.5021947345667155, 1.0436910033222817e-17,
            0.5069117244448543, 2.6093224822666467e-17, 0.5116065687490621, 4.599153755672175e-18,
            0.5162794744484545, 3.993597374030638e-17, 0.5209306456241853, -2.7615937047360392e-17,
            0.5255602835229274, -1.3653500125278526e-17, 0.5301685866091216, 3.4099964433831067e-17,
            0.5347557506160276, 2.799633595493548e-17, 0.5393219685956089, -5.8147828887258444e-18,
            0.5438674309672835, 1.566639275394884e-18, 0.5483923255655732, 5.0729470664834495e-18,
            0.5528968376866777, -6.969249981127853e-18, 0.5573811501340064, 3.602725098439577e-18,
            0.5618454432626918, 5.224264191205835e-18, 0.5662898950231159, -1.3073005827005202e-17,
            0.5707146810034716, -1.297607364828527e-17, 0.575119974471388, -2.204516362751676e-17,
            0.5795059464146423, -3.512392518658145e-17, 0.5838727655809827, 1.244188380096514e-17,
            0.5882205985170861, -3.654700436030262e-17, 0.5925496096066716, 1.9754587782874098e-17,
            0.5968599611077938, 1.2744940683873387e-17, 0.6011518131893349, -2.4240537054448186e-17,
            0.6054253239667169, 1.0023100529684243e-18, 0.6096806495368553, -2.790288553971928e-17,
            0.6139179440123705, -5.004248046799149e-17, 0.6181373595550788, -2.4204995015660967e-17,
            0.6223390464087788, -5.3841263063986186e-17, 0.6265231529313527, 1.5013273446326147e-17,
            0.6306898256261987, 1.9379643704799342e-17, 0.6348392091730102, 3.4802109413644295e-17,
            0.6389714464579207, 2.1544481596973328e-17, 0.6430866786030273, 5.345638872201408e-17,
            0.6471850449953096, -4.949129923505383e-17, 0.6512666833149581, 3.08814780504794e-17,
            0.6553317295631277, -5.2994846480875917e-17, 0.6593803180891278, 4.458191581087098e-17,
            0.6634125816170663, -3.705628907968261e-17, 0.6674286512719562, 2.624308761893046e-17,
            0.6714286566053024, -5.305290898108904e-17, 0.6754127256201767, -3.783326494807174e-18,
            0.6793809847957973, 1.2130957243298381e-17, 0.6833335591116206, 5.171399491038592e-17,
            0.6872705720709603, -4.9715488853809524e-17, 0.691192145724142, -4.528880016503407e-17
        };

        // 2^(j / 128) for j = 0..127, as hi, lo pairs
        static readonly double[] Exp2J =
        {
            1.0, 0.0, 1.0054299011128027, 9.499186535455032e-17,
            1.0108892860517005, -1.5234778603368577e-17, 1.016378314910953, -5.77217007319966e-17,
            1.0218971486541166, 5.109225028973444e-17, 1.0274459491187637, -4.9560741746453704e-17,
            1.0330248790212284, 7.600838874027088e-18, 1.0386341019613787, 5.996273788852511e-17,
            1.0442737824274138, 8.551889705537965e-17, 1.0499440858006872, 5.592937848127003e-17,
            1.0556451783605572, 1.759325738772092e-18, 1.061377227289262, -1.1973537085365658e-17,
            1.0671404006768237, -7.899853966841582e-17, 1.0729348675259756, -3.839668843358824e-18,
            1.0787607977571199, -6.656660436056593e-17, 1.0846183622133092, 3.166152845816346e-17,
            1.0905077326652577, -3.046782079812471e-17, 1.0964290818163769, -5.919933484449316e-17,
            1.102382583307841, 5.2660368715706944e-17, 1.1083684117236787, -8.786813845180527e-17,
            1.1143867425958924, 1.0410278456845571e-16, 1.1204377524096067, -6.201085906554179e-17,
            1.1265216186082418, 5.165856758795457e-17, 1.1326385195987192, 3.237356166738e-17,
            1.1387886347566916, 8.912812676025408e-17, 1.1449721444318042, 4.6412898921700107e-17,
            1.1511892299529827, 3.250710218863827e-17, 1.1574400736337511, -9.1238712311344e-17,
            1.1637248587775775, 3.8292048369240935e-17, 1.1700437696832502, -1.8477442017900047e-18,
            1.1763969916502812, 5.554203254218079e-17, 1.182784710984341, 1.542975430079076e-17,
            1.189207115002721, 3.982015231465646e-17, 1.1956643920398273, 4.6166036704814814e-17,
            1.202156731452703, 6.644981499252301e-17, 1.2086843236265816, -4.746725945228984e-17,
            1.215247359980469, -7.712630692681488e-17, 1.2218460329727576, -1.0611021211402691e-16,
            1.22848053610687, -1.89878163130253e-17, 1.2351510639369334, -1.0755244344307841e-16,
            1.241857812073484, 4.658027591836937e-17, 1.2486009771892048, -8.261810999021964e-17,
            1.255380757024691, -6.7113898212968784e-18, 1.2621973503942507, -3.0844648874738465e-17,
            1.2690509571917332, 2.667932131342186e-18, 1.275941778396392, 9.91543024421429e-17,
            1.2828700160787783, 1.713594918243561e-17, 1.2898358734066657, 8.949257530897592e-17,
            1.2968395546510096, 2.5382502794888315e-17, 1.3038812651919358, 8.647675598267871e-17,
            1.3109612115247644, -7.181536135519454e-17, 1.318079601266064, -5.4579558271491535e-17,
            1.3252366431597413, -2.8587312100388614e-17, 1.3324325470831615, -5.101586630916744e-17,
            1.339667524053303, 8.927282594831732e-17, 1.3469417862329458, 3.224065101254679e-17,
            1.3542555469368927, 7.70094837980299e-17, 1.3616090206382248, 1.533787661270668e-18,
            1.3690024229745905, 9.593797919118849e-17, 1.3764359707545302, -6.898588935871801e-17,
            1.383909881963832, -6.770511658794786e-17, 1.3914243757719262, -4.9061748652889893e-17,
            1.3989796725383112, -9.614213209051323e-17, 1.4065759938190154, 7.034914812136422e-18,
            1.4142135623730951, -9.667293313452913e-17, 1.4218926021691656, -1.6077828915890244e-17,
            1.42961333839197, -1.2031642489053655e-17, 1.4373759974489824, -4.2040340164675566e-17,
            1.4451808069770467, -3.0237581349939873e-17, 1.4530279958490526, -5.779948609396106e-17,
            1.460917794180647, -5.600377186075216e-17, 1.4688504333369818, 8.465882756533628e-17,
            1.4768261459394993, -3.483994556892796e-17, 1.4848451658727524, 1.0780086764407481e-16,
            1.4929077282912648, 1.4192920154284036e-17, 1.5010140696264256, -6.413767275790235e-17,
            1.5091644275934228, -1.016455327754295e-16, 1.5173590411982147, -4.308699472043341e-17,
            1.5255981507445384, -1.1024941712342561e-16, 1.533881997840956, 8.875226844438446e-17,
            1.5422108254079407, 7.949834809697621e-17, 1.550584877685, -1.4600706590689385e-17,
            1.559004400237837, 3.7812070533575275e-17, 1.567469639965553, -1.0352061768849722e-16,
            1.5759808451078865, -1.0136916471278304e-17, 1.5845382652524937, -1.9337717034585703e-17,
            1.593142151342267, -1.0094406542311964e-16, 1.6017927556826934, -6.054917453527784e-17,
            1.6104903319492543, 2.4707192569797888e-17, 1.6192351351948637, 2.0941334154229092e-17,
            1.6280274218573478, -6.712955084707084e-17, 1.6368674497669644, 7.698325071319876e-17,
            1.645755478153965, -1.0125679913674773e-16, 1.6546917676561943, 9.643294303196029e-17,
            1.6636765803267364, 5.8909926967131e-17, 1.6727101796415966, -5.476715964599563e-17,
            1.681792830507429, 8.199010020581497e-17, 1.6909247992693053, -9.66967147439488e-17,
            1.7001063537185235, -8.0237193703977e-18, 1.709337763100463, -9.868779456632931e-17,
            1.718619298122478, -1.851380418263111e-17, 1.7279512309618377, -1.0750981861204642e-16,
            1.7373338352737062, 3.164389299292957e-17, 1.746767386199169, -1.0752290483507515e-16,
            1.7562521603732995, 2.960140695448873e-17, 1.7657884359332727, 9.461315018083268e-17,
            1.7753764925265212, 6.429731796556572e-17, 1.785016611318935, 1.5330400121031314e-17,
            1.7947090750031072, 1.8227458427912087e-17, 1.804454167806624, -5.177222408793318e-17,
            1.8142521755003989, -9.969531538920349e-17, 1.8241033854070534, -1.0159627862277083e-16,
            1.8340080864093424, 3.283107224245627e-17, 1.843966568958626, -5.939742026949965e-17,
            1.8539791250833855, 9.761887490727594e-17, 1.864046048397789, 6.540912680620572e-17,
            1.8741676341103, -6.122763413004143e-17, 1.8843441790323345, -8.226593125533711e-17,
            1.8945759815869656, 3.4034035352165297e-17, 1.9048633418176741, 6.533857514718279e-17,
            1.9152065613971474, -1.0619946056195963e-16, 1.925605943636125, -9.914963769693741e-17,
            1.9360617934922943, 1.0332385960676326e-16, 1.9465744175792332, 6.811022349533877e-17,
            1.9571441241754002, 8.960767791036668e-17, 1.9677712232331759, -1.0314928011531132e-16,
            1.978456026387951, 4.0388753109278167e-17, 1.9891988469672663, 8.2051326383692e-18
        };
    }
}
