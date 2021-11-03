using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Net.Http;
using Newtonsoft.Json;
using System.Collections.Concurrent;
using System.Diagnostics;
using Math.Gmp.Native;
using ILGPU;

namespace Euler
{
    class Program
    {
        private static readonly HttpClient client = new();
        private readonly List<double>[] notPossible = new List<double>[10];
        private readonly int numTask = 10;


        /**
         *                       _____________________
         *                      ∕        c           ∕|
         *                    b∕                   b∕ |
         *                    ∕                    ∕  |a
         *                   ∕                    ∕   |
         *                  ∕____________________∕    |
         *                 |         c          |    ∕
         *                 |                    |  b∕
         *                 |a                  a|  ∕
         *                 |                    | ∕
         *                 |         c          |∕
         *                  ‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾
         *  d = √(a²+b²)
         *  e = √(a²+c²)
         *  f = √(b²+c²)
         *  g = √(a²+b²+c²)
         *  
         *  a is given by the task manager
         *  
         *  first : find d by incrementing b
         *  second : when d is integer, increment c until finding an integer for e
         *  third : when e is integer, calculte f
         *  last : if f is integer, calculate g
         **/


        static async Task Main(string[] args)
        {
            Program program = new Program();
            program.InitNotPossible();

            using var context = new Context();

            // Enable all algorithms and extension methods
            context.EnableAlgorithms();

            while (true)
            {
                await program.Calcul();
            }
        }

        async Task Calcul()
        {

            // get a task from the server
            TaskParam task = await GetTask();


             // create a container for the results
             ConcurrentBag <Task<double[]>> results = new ConcurrentBag<Task<double[]>>();

            if (task.b == 0)
            {
                // b is always superior to a 
                // calculate the max value of b that gives for d a result >= b+1
                // we want √(a²+b²) >= (b+1)
                // when b > ((a*a) - 1) / 2 then the result of the square root (d) is between b and b+1
                // we set the bEnd to this value



                double bEnd = System.Math.Floor((System.Math.Pow(task.a, 2) - 1) / 2); // ((a*a) - 1) / 2
                Stopwatch stopWatch = new();
                stopWatch.Start();
                double dint = 0;

                int lastDigitA = (int)task.a % 10; //2

                ParallelLoopResult result = Parallel.For(0, numTask, (i, state) => {

                    // get last digit of future "b"
                    double lastDigitB = ((lastDigitA + 1) + i) % 10;

                    // then check if the last digit of "b" is compatible with "a" and create loop only if needed
                    if (notPossible[lastDigitA].Contains(lastDigitB) == false)
                    {
                        results.Add(CalculAB(task, bEnd, (int)i));
                    }
                });

                // get the number of int returned by the tasks
                foreach (Task<double[]> resultat in results)
                {
                    dint += resultat.Result[0];
                }


                stopWatch.Stop();
                Console.Write("temps : "+ stopWatch.ElapsedMilliseconds);

                Console.Write("num int found : "+ dint);

                Console.WriteLine("--> End task for side 'a' -> " + task.a.ToString());

                Dictionary<string, string> resut = new()
                {
                    { "a", task.a.ToString() },
                    { "dint", dint.ToString() },
                    { "id", task.id }
                };
                FormUrlEncodedContent data2 = new(resut);

                await client.PostAsync("https://www.perfecteulerbrick.com/api/endTaskAB.php", data2);

            }
            else
            {
                // c is always superior to a and b 
                // calculate the max value of b that gives for f a result >= c+1
                // we want √(b²+c²) >= (c+1)
                // when c > ((b*b) - 1) / 2 then the result of the square root (f) is between c and c+1
                // we set the cEnd to this value

                double cEnd = System.Math.Floor((System.Math.Pow(task.b, 2) - 1) / 2); // ((a*a) - 1) / 2
                Stopwatch stopWatch = new();
                stopWatch.Start();
                double eint = 0;
                double fint = 0;

                int lastDigitA = (int)task.a % 10;
                int lastDigitB = (int)task.b % 10;

                ParallelLoopResult result = Parallel.For(0, numTask, (i, state) => {

                    // get last digit of future "c"
                    double lastDigitC = ((lastDigitB + 1) + i) % 10;

                    // then check if the last digit of "b" is compatible with "a" and create loop only if needed
                    if (notPossible[lastDigitA].Contains(lastDigitC) == false && notPossible[lastDigitB].Contains(lastDigitC) == false)
                    {
                        results.Add(CalculC(task, cEnd, (int)i));
                    }
                });

                // get the number of int returned by the tasks
                foreach (Task<double[]> resultat in results)
                {
                    eint += resultat.Result[0];
                    fint += resultat.Result[1];
                }


                stopWatch.Stop();
                Console.Write("temps : " + stopWatch.ElapsedMilliseconds);

                Console.Write("num int found e: " + eint);
                Console.Write("num int found f: " + fint);

                Console.WriteLine("--> End task for 'a|b' -> " + task.a.ToString()+"|"+ task.b.ToString());

                Dictionary<string, string> resut = new()
                {
                    { "a", task.a.ToString() },
                    { "b", task.b.ToString() },
                    { "eint", eint.ToString() },
                    { "fint", fint.ToString() },
                    { "id", task.id }
                };
                FormUrlEncodedContent data = new(resut);

                await client.PostAsync("https://www.perfecteulerbrick.com/api/endTaskC.php", data);
            }
        }
        async Task<double[]> CalculAB(TaskParam task, double bEnd, int increment)
        {

            Console.WriteLine("Start task side 'a', 'increment' -> " + task.a.ToString()+" - "+increment.ToString());



            double a = task.a;
            double dint = 0;

            string id = task.id;
            double acarre = a * a;



            for (double b = (a + 1 + increment); b <= bEnd; b += numTask)
            {
                double dcarre = ((acarre) + (b * b));
                double d = System.Math.Sqrt(dcarre);

                // if d is int
                if ((d == (Int64)d) && (d * d) == dcarre)
                {
                    // check with high precision (slow but exact)
                    bool sqrtPrecision = SqrtPrecision(dcarre);

                    if (sqrtPrecision == false)
                    {
                        continue;
                    }

                    dint++;

                    Dictionary<string, string> values = new()
                    {
                        { "a", a.ToString() },
                        { "b", b.ToString() },
                        { "d", d.ToString() },
                        { "id", id }
                    };
                    FormUrlEncodedContent data = new(values);

                    await client.PostAsync("https://www.perfecteulerbrick.com/api/foundInt.php", data);
                }
            }

            double[] retour = new double[1];
            retour[0] = dint;

            return retour;
        }

        async Task<double[]> CalculC(TaskParam task, double cEnd, int increment)
        {

            Console.WriteLine("Start task side 'a, b' -> " + task.a.ToString() + " " + task.b.ToString());


            double a = task.a;
            double b = task.b;

            double eint = 0;

            string id = task.id;
            double numBrickEuler = 0;

            double acarre = a * a;
            double bcarre = b * b;

            // test if the last digit of a and b are "compatible" to produce a int square root
            for (double c = (b + 1 + increment); b <= cEnd; c += numTask)
            {
                double ccarre = c * c;

                double ecarre = acarre + ccarre;
                double e = System.Math.Sqrt(ecarre);

                // if d is int
                if ((e == (Int64)e) == true)
                {
                    // check with high precision (slow but exact)
                    bool sqrtPrecision = SqrtPrecision(ecarre);

                    if (sqrtPrecision == false)
                    {
                        continue;
                    }
                    eint++;

                    double fcarre = bcarre + ccarre;
                    double f = System.Math.Sqrt(fcarre);

                    if ((f == (Int64)f) == true)
                    {
                        // check with high precision (slow but exact)
                        sqrtPrecision = SqrtPrecision(fcarre);

                        if (sqrtPrecision == false)
                        {
                            continue;
                        }

                        Dictionary<string, string> values = new()
                        {
                                { "a", a.ToString() },
                                { "b", b.ToString() },
                                { "c", c.ToString() },
                                { "e", e.ToString() },
                                { "f", f.ToString() },
                                { "id", id }
                            };
                        FormUrlEncodedContent data = new(values);

                        await client.PostAsync("https://www.perfecteulerbrick.com/api/brickFound.php", data);

                        double gcarre = acarre + bcarre + ccarre;
                        double g = System.Math.Sqrt(gcarre);
                        if ((g == (Int64)g) == true)
                        {
                            // check with high precision (slow but exact)
                            sqrtPrecision = SqrtPrecision(gcarre);

                            if (sqrtPrecision == false)
                            {
                                continue;
                            }

                            Dictionary<string, string> theBrick = new Dictionary<string, string>
                            {
                                { "a", a.ToString() },
                                { "b", b.ToString() },
                                { "c", c.ToString() },
                                { "e", e.ToString() },
                                { "f", e.ToString() },
                                { "g", e.ToString() },
                                { "id", id }
                            };
                            FormUrlEncodedContent dataTheBrick = new(theBrick);

                            // don't try calling this URL with incorrect values
                            await client.PostAsync("https://www.perfecteulerbrick.com/api/perfectEulerBrickFound.php", dataTheBrick);
                        }
                    }
                }
            }
            double[] retour = new double[2];
            retour[0] = eint;
            retour[1] = numBrickEuler;
            return retour;
        }


        /***
         * init the notPossible array that contain a list of numberd
         * giving impossible int when squared/sumed then squared root
         * numbers that finish with a 2, 3, 7, 8 don't have int square root
         * so, if the sum of last digit of 2 numebrs gives a result that last with those numbers
         * we don't need to calculate the square root
         * if "a" last with a 1, b can't finish by a 1, 4, 6 or 9
         * etc...
         */
        /// <summary>
        /// init the notPossible array
        /// </summary>
        private void InitNotPossible()
        {
            notPossible[0] = new List<double>() { };
            notPossible[1] = new List<double>() { 1, 4, 6, 9 };
            notPossible[2] = new List<double>() { 2, 3, 7, 8 };
            notPossible[3] = notPossible[2];
            notPossible[4] = notPossible[1];
            notPossible[5] = notPossible[0];
            notPossible[6] = notPossible[1];
            notPossible[7] = notPossible[2];
            notPossible[8] = notPossible[2];
            notPossible[9] = notPossible[1];

        }

        static bool SqrtPrecision(double x)
        {
            mpf_t op = new mpf_t();

            gmp_lib.mpf_init_set_d(op, x);

            mpf_t rop = new mpf_t();
            gmp_lib.mpf_init(rop);

            // Set rop = trunc(sqrt(op)).
            gmp_lib.mpf_sqrt(rop, op);
            if (gmp_lib.mpf_integer_p(rop) == 0)
            {

                return false;
            }

            return true;
        }

        static async Task<TaskParam> GetTask()
        {
            JsonSerializerSettings settings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                MissingMemberHandling = MissingMemberHandling.Ignore
            };

            string responseString = await client.GetStringAsync("https://www.perfecteulerbrick.com/api/getTask.php").ConfigureAwait(true);
            return JsonConvert.DeserializeObject<TaskParam>(responseString, settings);
        }
    }

    class TaskParam
    {
        public double a;
        public double b;
        public string id;
    }
}
