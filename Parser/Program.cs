using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;

namespace Parser
{
    class Program
    {
        static String mainUrl = "https://ebrowarium.pl/";

        static void Main(string[] args)
        {
            DateTime start = DateTime.Now;
            HtmlWorker html = new HtmlWorker();

            Console.WriteLine("Parsing " + mainUrl + " ...");
            html.GetDataFromIndex(html.GetHtml(mainUrl));
            DateTime end = DateTime.Now;
            var diff = end.Subtract(start);
            Console.WriteLine("Done! Time: " + String.Format("{0}:{1}:{2}", diff.Hours,diff.Minutes,diff.Seconds));
        }
    }
}
