using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace HanabePhotoManager.App.Contest;

public enum ContestStatus { Open, Judged }

public sealed partial class ContestItem : ObservableObject
{
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public string Url { get; init; } = "";
    public ContestStatus Status { get; init; }
    public string Category { get; init; } = "";
}

public sealed partial class ContestViewModel : ObservableObject
{
    public ObservableCollection<ContestItem> OpenContests { get; } = new();
    public ObservableCollection<ContestItem> JudgedContests { get; } = new();

    public ContestViewModel()
    {
        OpenContests.Add(new ContestItem
        {
            Name = "Sony World Photography Awards",
            Description = "全球最具影响力的摄影赛事之一，分专业组、公开组、青年组和学生组。奖金最高 $25,000。",
            Url = "https://www.worldphoto.org/sony-world-photography-awards",
            Status = ContestStatus.Open,
            Category = "综合"
        });
        OpenContests.Add(new ContestItem
        {
            Name = "国家地理旅行摄影大赛",
            Description = "年度旅行摄影大赛，分自然、人物、城市三大类别。获奖作品刊登于国家地理杂志。",
            Url = "https://www.nationalgeographic.com/travel/article/photo-contest",
            Status = ContestStatus.Open,
            Category = "旅行"
        });
        OpenContests.Add(new ContestItem
        {
            Name = "Drone Photo Awards · 国际无人机摄影大赛",
            Description = "全球最重要的航拍摄影大赛，涵盖自然、人文、城市、抽象等9个类别。",
            Url = "https://droneawards.photo",
            Status = ContestStatus.Open,
            Category = "航拍"
        });
        OpenContests.Add(new ContestItem
        {
            Name = "Weather Photographer of the Year",
            Description = "英国皇家气象学会主办，展示全球最佳天气与气候摄影作品。",
            Url = "https://www.rmets.org/weather-photographer-of-the-year",
            Status = ContestStatus.Open,
            Category = "自然"
        });
        OpenContests.Add(new ContestItem
        {
            Name = "MPA 手机摄影大赛 (Mobile Photography Awards)",
            Description = "全球最大的手机摄影赛事，20+个类别，涵盖人像、风光、街拍等。",
            Url = "https://mobilephotoawards.com",
            Status = ContestStatus.Open,
            Category = "手机摄影"
        });
        OpenContests.Add(new ContestItem
        {
            Name = "平遥国际摄影大展",
            Description = "中国规模最大、历史最悠久的国际摄影展会之一，每年9月在山西平遥古城举办。",
            Url = "http://www.pip919.com",
            Status = ContestStatus.Open,
            Category = "综合"
        });
        OpenContests.Add(new ContestItem
        {
            Name = "大理国际影会",
            Description = "中国西南地区最具影响力的国际摄影节，集展览、论坛、赛事为一体。",
            Url = "https://www.dipephoto.com",
            Status = ContestStatus.Open,
            Category = "综合"
        });

        JudgedContests.Add(new ContestItem
        {
            Name = "2025 World Press Photo · 世界新闻摄影奖",
            Description = "全球最权威的新闻摄影大赛。2025年度大奖：Mohammed Salem 拍摄的巴勒斯坦妇女拥抱侄女遗体。",
            Url = "https://www.worldpressphoto.org/collection/photocontest/2025",
            Status = ContestStatus.Judged,
            Category = "新闻纪实"
        });
        JudgedContests.Add(new ContestItem
        {
            Name = "2025 IPPAWARDS · iPhone 摄影奖",
            Description = "全球首个iPhone摄影奖。2025年度摄影师：Yajun Hu（胡亚军，中国），作品《编辫子》。",
            Url = "https://www.ippawards.com",
            Status = ContestStatus.Judged,
            Category = "手机摄影"
        });
        JudgedContests.Add(new ContestItem
        {
            Name = "2025 Hasselblad Masters · 哈苏大师奖",
            Description = "哈苏相机旗舰摄影奖项，每两年评选一次。2025年六位大师涵盖人像、风光、建筑等类别。",
            Url = "https://www.hasselblad.com/masters",
            Status = ContestStatus.Judged,
            Category = "专业摄影"
        });
        JudgedContests.Add(new ContestItem
        {
            Name = "2024 Wildlife Photographer of the Year",
            Description = "伦敦自然历史博物馆主办的全球顶级自然摄影大赛。2024年大奖：Nima Sarikhani 作品《冰床》。",
            Url = "https://www.nhm.ac.uk/wpy",
            Status = ContestStatus.Judged,
            Category = "自然"
        });
        JudgedContests.Add(new ContestItem
        {
            Name = "2025 LOBA · 徕卡奥斯卡巴纳克奖",
            Description = "徕卡相机旗舰摄影奖，表彰具有人文关怀的纪实摄影师。2025年大奖：Davide Monteleone。",
            Url = "https://www.leica-oskar-barnack-award.com",
            Status = ContestStatus.Judged,
            Category = "人文纪实"
        });
        JudgedContests.Add(new ContestItem
        {
            Name = "2024 Prix Pictet 摄影奖",
            Description = "以人类为主题的顶级摄影奖，关注全球可持续发展议题。2024年大奖：Gauri Gill。",
            Url = "https://www.prixpictet.com",
            Status = ContestStatus.Judged,
            Category = "人文"
        });
        JudgedContests.Add(new ContestItem
        {
            Name = "2024 中国摄影金像奖",
            Description = "中国摄影界最高个人成就奖，由中国文联和中国摄影家协会主办。分记录类、艺术类、商业类。",
            Url = "https://www.cpanet.org.cn",
            Status = ContestStatus.Judged,
            Category = "综合"
        });
        JudgedContests.Add(new ContestItem
        {
            Name = "Fujifilm Street Photography Awards · 富士街头摄影奖",
            Description = "全球街头摄影领域的标杆赛事，2024年冠军：Jutharat Pinyodoonyachet。",
            Url = "https://fujifilm-x.com/global/special/fujifilm-x-pro3-street-awards",
            Status = ContestStatus.Judged,
            Category = "街拍"
        });
    }
}
