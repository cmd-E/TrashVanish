﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TrashVanish.Classes
{
    internal class SetModel
    {
        public string setID { get; set; }
        public string setName { get; set; }
        public List<setExtensionModel> extensions { get; set; }
    }
}