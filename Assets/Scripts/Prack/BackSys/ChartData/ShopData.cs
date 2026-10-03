using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Assets.Scripts.Prack.BackSys.ChartData
{
    [System.Serializable]
    public class ShopData
    {
        public int ShopIdx;
        public string GoodsNm;
        public int PayKind;
        public int PayVal;
        public int ReceiptKind;
        public int ReceiptVal;
        public int Ord;
        public string IsUse;

        public override string ToString()
        {
            string result = string.Empty;
            result += $"ShopIdx : {ShopIdx}, ";
            result += $"GoodsNm : {GoodsNm}, ";
            result += $"PayKind : {PayKind}, ";
            result += $"PayVal : {PayVal}, ";
            result += $"ReceiptKind : {ReceiptKind}, ";
            result += $"ReceiptVal : {ReceiptVal}, ";
            result += $"Ord : {Ord}, ";
            result += $"IsUse : {IsUse}";

            return result;
        }

    }
}
