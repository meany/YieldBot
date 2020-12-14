using dm.YLD.Common;
using dm.YLD.Data.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace dm.YLD.Data
{
    public static class Utils
    {
        public static LPPair GetLPPair(string contract)
        {
            if (contract == Statics.TOKEN_UNISWAP_ETH)
                return LPPair.ETH_YLD;

            if (contract == Statics.TOKEN_UNISWAP_RFI)
                return LPPair.RFI_YLD;

            throw new Exception($"Contract address ({contract}) does not match any LP tokens.");
        }
    }
}
