using UnityEngine;
using UnityEngine.UI;

// Draws a kit from plain UI Images (DRAFT). Build one prefab:
//  - shirt body, two sleeves, a collar, shorts and two socks as Images
//  - stripeBars: 3 tall bars across the shirt; hoopBars: 3 wide bars; halfBar: right half; sashBar: a diagonal bar
//    (rotate the sash bar in the prefab). Put the pattern bars above the shirt and below the sleeves.
// Call Apply(kit) whenever the club or kit changes.
public class KitView : MonoBehaviour
{
    [SerializeField] private Image shirt;
    [SerializeField] private Image sleeveLeft;
    [SerializeField] private Image sleeveRight;
    [SerializeField] private Image collar;
    [SerializeField] private Image shorts;
    [SerializeField] private Image sockLeft;
    [SerializeField] private Image sockRight;
    [SerializeField] private Image[] stripeBars;
    [SerializeField] private Image[] hoopBars;
    [SerializeField] private Image halfBar;
    [SerializeField] private Image sashBar;

    public void Apply(ClubKit kit)
    {
        ClubKit k = KitCatalog.Sanitize(kit ?? new ClubKit());
        Set(shirt, k.body); Set(sleeveLeft, k.sleeve); Set(sleeveRight, k.sleeve); Set(collar, k.trim);
        Set(shorts, k.shorts); Set(sockLeft, k.socks); Set(sockRight, k.socks);

        Show(stripeBars, k.pattern == "stripes", k.accent);
        Show(hoopBars, k.pattern == "hoops", k.accent);
        Show(halfBar, k.pattern == "halves", k.accent);
        Show(sashBar, k.pattern == "sash", k.accent);
    }

    public void Apply(UserProfile p) { Apply(KitCatalog.ForProfile(p)); }

    private static void Set(Image img, string hex) { if (img != null) img.color = KitCatalog.ToColor(hex); }

    private static void Show(Image img, bool on, string hex)
    {
        if (img == null) return;
        img.gameObject.SetActive(on);
        if (on) img.color = KitCatalog.ToColor(hex);
    }

    private static void Show(Image[] imgs, bool on, string hex)
    {
        if (imgs == null) return;
        foreach (Image img in imgs) Show(img, on, hex);
    }
}
