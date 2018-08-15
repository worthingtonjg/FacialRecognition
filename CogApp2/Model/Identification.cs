using Microsoft.Azure.CognitiveServices.Vision.Face.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CogApp2.Model
{
    public class Identification
    {
        public Person Person { get; set; }
        public IdentifyResult IdentifyResult { get; set; }
        public DetectedFace Face { get; set; }

        public double Confidence;
    }
}
