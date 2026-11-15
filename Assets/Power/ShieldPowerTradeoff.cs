namespace SpaceDilemma.Power
{
    // Compare actual supply against no-shield supply and the requests before SPE.
    // Pre-existing shortages and devices already switched off are not blamed on shielding.
    public sealed class ShieldPowerTradeoff
    {
        PowerBudget beforeSpe, withoutShield;
        readonly bool[,] interrupted = new bool[3,3];
        public bool Interrupted(int mission,int device) => interrupted[mission,device];
        public void Observe(PowerBudget budget,bool speActive)
        {
            if(beforeSpe==null) beforeSpe=budget.CopyWithoutShield();
            if(withoutShield==null) withoutShield=budget.CopyWithoutShield();
            else withoutShield.CopyRequestsFrom(budget);
            if(!speActive) beforeSpe.CopyRequestsFrom(budget);
            for(int m=0;m<3;m++)for(int d=0;d<3;d++)
            {
                bool manualCut=beforeSpe.Powered(m,d) && (!budget.RequestedOn(m,d) ||
                    budget.Requested(m)<beforeSpe.Requested(m));
                interrupted[m,d]=speActive && !budget.Powered(m,d) &&
                    (manualCut || (budget.ShieldActualOutput>0 && withoutShield.Powered(m,d)));
            }
        }
    }
}
