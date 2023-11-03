using System;
using System.Net;
using System.Xml;
using HtmlAgilityPack;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;

namespace Parser
{
    class HtmlWorker
    {
        enum BeerCategory
        {
            ALE,
            BEZALKOHOLOWE,
            BEZGLUTENOWE,
            CIEMNE,
            JASNE,
            KLASZTORNE,
            KOZLAKI,
            NIEFILTROWANE,
            NIEPASTERYZOWANE,
            PORTERY,
            PSZENICZNE,
            SMAKOWE,
            WEDZONE,
            WIELOZBOZOWE,

            INVALID_CAT
        }

        public class BeerData
        {
            public BeerData(
                string category,
                string name,
                string producer,
                string description,
                string style,
                string alc,
                string bottle,
                List<string> image,
                int quantity,
                string blg,
                string price)

            {
                mCategory = category.Replace(";", ".");
                mName = name.Replace(";", ".");
                mProducer = producer.Replace(";", ".");
                mDescription = description.Replace(";", ".");
                mStyle = style.Replace(";", ".");
                mBlg = blg.Replace(";", ".");
                mAlc = alc.Replace(";", ".");
                mBottle = bottle.Replace(";", ".");
                imgUrl = image;
                mQuantity = quantity;
                mPrice = price.Replace(";", ".");
            }

            public string mCategory;
            public string mName;
            public string mProducer;
            public string mDescription;
            public string mStyle;
            public string mBlg;
            public string mAlc;
            public string mBottle;
            public List<String> imgUrl;
            public int mQuantity;
            public string mPrice;
        };

        public String GetHtml(String url)
        {
            WebClient http = new WebClient();
            var page = http.DownloadString(url);
            return page;
        }

        /// <summary>
        /// Harvest for data from main page
        /// </summary>
        /// <param name="html">Url to main page</param>
        /// <returns>Retun list of links to pages with beer description</returns>
        public void GetDataFromIndex(String html)
        {
            var result = new List<BeerData>();

            HtmlDocument document = new HtmlDocument();
            document.LoadHtml(html);

            HtmlNode node = document.GetElementbyId("kategorieInfoBox");
            List<HtmlNode> catNodes = node.DescendantsAndSelf("a").ToList().Where(
                currnode => currnode.Attributes.Any(
                    attribute => attribute.Name == "title" && 
                    attribute.Value.Contains("rodzaje"))).ToList();

            foreach (HtmlNode catNode in catNodes)
            {
                String subPageUrl = catNode.Attributes.Where(attr => attr.Name == "href").First().Value;
                result.AddRange(GetDataFromCategoryPage(subPageUrl));
            }

            DataToFile(result);
        }

        private List<BeerData> GetDataFromCategoryPage(String subPageUrl)
        {
            Console.WriteLine("\tParsing: " + subPageUrl);

            var result = new List<BeerData>();

            HtmlDocument subDocument = new HtmlDocument();
            subDocument.LoadHtml(GetHtml(subPageUrl));

            HtmlNode mainSubNode = subDocument.GetElementbyId("srodkowaKolumna");
            List<HtmlNode> subNodes = mainSubNode.DescendantNodes(50).ToList();
            List<HtmlNode> subCategory = subNodes.Where(dNode => dNode.Name == "a").ToList();
            List<string> subCategoriesLinks = subCategory.Select(
                categoryNode => categoryNode.Attributes["href"].Value).Distinct().ToList();

            foreach (var subcategoryLink in subCategoriesLinks)
            {
                result.AddRange(GetAllForSubCategory(subcategoryLink));
            }

            return result;
        }

        private List<BeerData> GetAllForSubCategory(string singleSubUrl)
        {
            Console.WriteLine("\t  Data from : " + singleSubUrl);

            var result = new List<BeerData>();

            string tmpUrl = singleSubUrl;
            int pageCnt = 1;

            while(true)
            {
                HtmlDocument document = new HtmlDocument();
                document.LoadHtml(GetHtml(tmpUrl));

                HtmlNode mainSubNode = document.GetElementbyId("srodkowaKolumna");
                string category = mainSubNode.Descendants("h1").First().InnerText;

                List<HtmlNode> subNodes = mainSubNode.DescendantNodes(50).ToList();
                List<HtmlNode> subCategory = subNodes.Where(dNode => dNode.Name == "a").ToList();
                List<HtmlNode> beerCategoryOnly = subCategory.Where(
                    dNode => dNode.Attributes.Any(att => att.Value == "podgladMiniaturek")).ToList();
                List<string> beerLinks = beerCategoryOnly.Select(
                    categoryNode => categoryNode.Attributes["href"].Value).Distinct().ToList();

                foreach (string link in beerLinks)
                {
                    result.Add(GetBeerData(category, link));
                }

                List<HtmlNode> NavigationButtons = subCategory.Where(
                    dNode => dNode.Attributes.Any(att => att.Value == "pageResults")).ToList();
                var nextPageButtons =  NavigationButtons.Where(
                    button => button.Attributes.Any(att => att.Value == " NastÄ™pna strona ")).ToList();

                if (nextPageButtons.Count == 0)
                {
                    break;
                }
                
                pageCnt++;
                tmpUrl = singleSubUrl + "?page=" + pageCnt.ToString();
            }

            return result;
        }

        private BeerData GetBeerData(string category, string url)
        {
            try
            {
                HtmlDocument document = new HtmlDocument();
                document.LoadHtml(GetHtml(url));

                string name = document.GetElementbyId("nazwa_produktu").InnerText;

                List<string> imgUrls = new List<string>();
                List<HtmlNode> imgNodes = document.GetElementbyId("galeriaProduktu").Descendants("img").ToList();
                foreach (var node in imgNodes)
                {
                    string img = "https://ebrowarium.pl/" + node.Attributes.Where(attr => attr.Name == "src").First().Value;
                    imgUrls.Add(img);
                }

                string description = "";
                HtmlNode descriptionNode = document.GetElementbyId("opis");
                if (descriptionNode != null &&
                    descriptionNode.Descendants("span").ToList().Any())
                {
                    description = document.GetElementbyId("opis").Descendants("span").ToArray()[0].InnerText;
                }

                string producer = 
                    document.GetElementbyId("szczegolyProduktu").Descendants("a").ToArray()[1].InnerText.Replace("\t", "").Replace("\r\n", "");
                int quantity = Int32.Parse(document.GetElementbyId("products_quantity").Descendants("span").ToArray()[0].InnerText.Split('(').ToArray()[1].Split(' ').ToArray()[0]);

                HtmlNode attributes = document.GetElementbyId("atrybuty");

                string price = document.GetElementbyId("cena").Descendants("span").ToArray()[0].InnerText;

                String style = "", blg = "", alc = "", vol = "";
                if (attributes != null)
                {
                    List<HtmlNode> currAttribs = attributes.Descendants("div").Where(div => div.ChildNodes.Any(currChild => currChild.Name == "span")).ToList();

                    style = FindBeerAttribDefinition(ref currAttribs, "Style");
                    blg   = FindBeerAttribDefinition(ref currAttribs, "BLG");
                    alc   = FindBeerAttribDefinition(ref currAttribs, "Alkohol");
                    vol   = FindBeerAttribDefinition(ref currAttribs, "Volume");
                }

                BeerData foundBeer = new BeerData(category, name, producer, description, style, alc, vol, imgUrls, quantity, blg, price);


                Console.WriteLine("\t\t Got : " + name);

                return foundBeer;
            }
            catch(Exception e)
            { }

            return null;
        }

        private string FindBeerAttribDefinition(ref List<HtmlNode> currAttribs, String beerAttrib)
        {
            string result = "";

            var foundBeerAttrib = currAttribs.FirstOrDefault(div => div.InnerHtml.Contains(beerAttrib));
            if (foundBeerAttrib != null)
            {
                var beerAttribDefinition = foundBeerAttrib.ChildNodes.First(
                    span => span.Attributes.Contains("class") && span.Attributes["class"].Value == "atributes_value");
                result = beerAttribDefinition.InnerText.Replace("\t", "").Replace("\r\n", "");
            } 
            
            return result;
        }

        private BeerCategory CategoryToEnum(string category)
        {
            if (category.Contains("Ale"))
                return BeerCategory.ALE;
            if (category.Contains("Bezalkoholowe"))
                return BeerCategory.BEZALKOHOLOWE;
            if (category.Contains("Bezglutenowe"))
                return BeerCategory.BEZGLUTENOWE;
            if (category.Contains("Ciemne"))
                return BeerCategory.CIEMNE;
            if (category.Contains("Jasne"))
                return BeerCategory.JASNE;
            if (category.Contains("Klasztorne"))
                return BeerCategory.KLASZTORNE;
            if (category.Contains("KoĹşlaki"))
                return BeerCategory.KOZLAKI;
            if (category.Contains("Niefiltrowane"))
                return BeerCategory.NIEFILTROWANE;
            if (category.Contains("Niepasteryzowane"))
                return BeerCategory.NIEPASTERYZOWANE;
            if (category.Contains("Portery"))
                return BeerCategory.PORTERY;
            if (category.Contains("Pszeniczne"))
                return BeerCategory.PSZENICZNE;
            if (category.Contains("Smakowe"))
                return BeerCategory.SMAKOWE;
            if (category.Contains("WÄ™dzone"))
                return BeerCategory.WEDZONE;

            return BeerCategory.WIELOZBOZOWE;
        }

        private void DataToFile(List<BeerData> beers)
        {
            Console.WriteLine("\tWriting to file...");

            StreamWriter sw = new StreamWriter("./beerData.csv");
            sw.WriteLine(string.Format("{0};{1};{2};{3};{4};{5};{6};{7};{8};{9};{10}",
                       "Kategoria",
                       "Nazwa",
                       "Producent",
                       "Opis",
                       "Styl",
                       "Alkohol",
                       "Butelka",
                       "Obrazki",
                       "Ilosc",
                       "BLG",
                       "Cena"));

            foreach (BeerData beer in beers)
            {
                if (beer == null)
                    continue;

                sw.WriteLine(string.Format("{0};{1};{2};{3};{4};{5};{6};{7};{8};{9};{10}",
                    Convert.ToInt32(CategoryToEnum(beer.mCategory)),
                    beer.mName,
                    beer.mProducer,
                    beer.mDescription,
                    beer.mStyle,
                    beer.mAlc,
                    beer.mBottle,
                    beer.imgUrl[0],
                    beer.mQuantity,
                    beer.mBlg,
                    beer.mPrice)
                    .Replace("Ä™", "ę")
                    .Replace("Ĺ‚", "ł")
                    .Replace("Ĺş", "ź")
                    .Replace("ĹĽ", "ż")
                    .Replace("&oacute.", "ó")
                    .Replace("&Oacute.", "Ó")
                    .Replace("Ĺ›", "ś")
                    .Replace("Ĺš", "Ś")
                    .Replace("Ä…", "ą")
                    .Replace("Ĺ„", "ń"));
            }
        }
    }
}
