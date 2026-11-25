namespace StarSigns;

public class ModConfig
{
    public float GoodMatchGain { get; set; } = 1.25f;
    public float GoodMatchLoss { get; set; } = 0.75f;
    public float ClashGain { get; set; } = 0.75f;
    public float ClashLoss { get; set; } = 1.25f;
    public int RealignCost { get; set; } = 2000;
    public bool ShowSignsOnSocialTab { get; set; } = true;
}
