using Newtonsoft.Json;
using SharpYaml.Serialization;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using YamlNodeExtensions;

namespace ScribanCsvTemplateEngine
{
    class YamlExtensionsTests
    {
        public static void LoadYamlStreamTest(string document)
        {
            // Setup the input
            var input = new StringReader(document);

            // Load the stream
            var yaml = new YamlStream();
            yaml.Load(input);

            // Examine the stream
            var pmapping = (YamlMappingNode)yaml.Documents[0].RootNode;

            var sw = new Stopwatch();
            sw.Start();
            var wut1 = pmapping.ToPoco();
            sw.Stop();

            sw.Reset();
            sw.Start();
            var wut2 = pmapping.ToPoco<DocumentModel>();
            sw.Stop();

            //Console.WriteLine(sw.ElapsedMilliseconds);
            var look1 = JsonConvert.SerializeObject(wut1, Formatting.Indented);
            var back1 = JsonConvert.DeserializeObject(look1).ToString();

            var look2 = JsonConvert.SerializeObject(wut2, Formatting.Indented);
            var back2 = JsonConvert.DeserializeObject(look2).ToString();

            Console.WriteLine(wut1.Receipt);

            Console.WriteLine(wut1.Date);

            Console.WriteLine($"{wut1.Customer.Family}, {wut1.Customer.Given}");

            Console.WriteLine(nameof(wut1.Items));
            foreach (var item in wut1.Items)
                Console.WriteLine($"{item.PartNo}, {item.Descrip}, {item.Price}, {item.Quantity}");

           
            var wutDict = (wut1 as IDictionary<string, object>);
            var billToKey = "BillTo";


if (!wutDict.ContainsKey(billToKey)) 
    goto escape_NoBillTo
            ;

            Console.WriteLine(billToKey);

            var billTo = (dynamic)wutDict[billToKey];
            Console.WriteLine($"{billTo.Street}, {billTo.City}, {billTo.State}");

            var shipToKey = "ShipTo";
            Console.WriteLine(shipToKey);

            var shipTo = (dynamic)wutDict[shipToKey];
            Console.WriteLine($"{shipTo.Street}, {shipTo.City}, {shipTo.State}");

            Console.WriteLine(nameof(wut1.SpecialDelivery));
            Console.WriteLine(wut1.SpecialDelivery);

escape_NoBillTo:
            ;

            Console.WriteLine("Test passed.");
        }

        private const string Document2 = @"---
            receipt:    Oz-Ware Purchase Invoice
            date:        2007-08-06
            customer:
                given:   Dorothy
                family:  Gale
            items:
                - part_no:   A4786
                  descrip:   Water Bucket (Filled)
                  price:     1.47
                  quantity:  4
                - part_no:   E1628
                  descrip:   High Heeled ""Ruby"" Slippers
                  price:     100.27
                  quantity:  1
...";

        public class CustomerModel
        {
            public string Given { get; set; }
            public string Family { get; set; }
        }

        public class ItemModel
        {
            public string PartNo { get; set; }
            public string Descrip { get; set; }
            public double Price { get; set; }
            public int Quantity { get; set; }
        }

        public class AddressModel
        {
            public string Street { get; set; }
            public string City { get; set; }
            public string State { get; set; }
        }

        public class DocumentModel
        {
            public string Recipt { get; set; }
            public DateTime Date { get; set; }
            public CustomerModel Customer { get; set; }
            public List<ItemModel> Items { get; set; }
            public AddressModel BillTo { get; set; }
            public AddressModel SendTo { get; set; }
            public string SpecialDelivery { get; set; }
        }

        private const string Document = @"---
            receipt:    Oz-Ware Purchase Invoice
            date:        2007-08-06
            customer:
                given:   Dorothy
                family:  Gale
            list:
              - item1
              - item2
            items:
                - part_no:   A4786
                  descrip:   Water Bucket (Filled)
                  price:     1.47
                  quantity:  4
                - part_no:   E1628
                  descrip:   High Heeled ""Ruby"" Slippers
                  price:     100.27
                  quantity:  1
            bill-to:  &id001
                street: |
                        123 Tornado Alley
                        Suite 16
                city:   East Westville
                state:  KS
            ship-to:  *id001
            specialDelivery:  >
                Follow the Yellow Brick
                Road to the Emerald City.
                Pay no attention to the
                man behind the curtain.
...";
    }
}
