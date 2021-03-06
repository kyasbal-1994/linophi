﻿
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Net.Mail;
using System.Net.Mime;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.Owin;
using Web.Models;
using Web.Storage;
using Web.Storage.Connection;
using Web.Utility;
using Web.ViewModel.Home;

namespace Web.Controllers
{
    public class HomeController : Controller
    {
        /// <summary>
        /// 記事表示用のVMを生成するメソッド
        /// </summary>
        /// <param name="articleId"></param>
        /// <returns></returns>
        private async Task<ViewArticleViewModel> getArticleViewModel(string articleId)
        {
            ApplicationDbContext context = HttpContext.GetOwinContext().Get<ApplicationDbContext>();
            var article =  await context.Articles.FindAsync(articleId);
            if (article == null) return null;
            await context.Entry(article).Collection(c=>c.Tags).LoadAsync();
            BlobStorageConnection bConnection = new BlobStorageConnection();
            TableStorageConnection tConnection = new TableStorageConnection();
            ArticleBodyTableManager manager=new ArticleBodyTableManager(bConnection);
            ArticleThumbnailManager thumbnail=new ArticleThumbnailManager(bConnection);
            LabelTableManager ltm=new LabelTableManager(tConnection);
            ArticleCommentTableManager actm=new ArticleCommentTableManager(tConnection);
            var author =
                await
                    HttpContext.GetOwinContext()
                        .GetUserManager<ApplicationUserManager>()
                        .FindByNameAsync(article.AuthorID);
            if (article.IsDraft)
            {
                if (article.AuthorID != User.Identity.Name) return null;
            }
            else
            {
                article.PageView++;
            }
            await context.SaveChangesAsync();
            IGravatarLoader gLoader = new BasicGravatarLoader(author.Email);
            int commentCount = 0;
            string commentsAsJson = actm.GetCommentsAsJson(articleId, out commentCount);
            int count = 0;
            return new ViewArticleViewModel()
            {
                ArticleId = articleId,
                Author=author.NickName,
                Author_ID=author.UniqueId,
                Author_IconTag=gLoader.GetIconTag(50),
                PageView=article.PageView,
                Title = article.Title,
                Content =await manager.GetArticleBody(article.ArticleModelId),
                LabelInfo=ltm.GetLabelsJson(articleId),
                Tags =await getArticleTagModels(article),
                LabelCount = article.LabelCount,
                Article_Date = article.CreationTime.ToShortDateString(),
                Article_UpDate = article.UpdateTime.ToShortDateString(),
                UseThumbnail= thumbnail.CheckThumbnailExist(articleId),
                CommentInfo=commentsAsJson,
                CommentCount=commentCount,
                RelatedArticles=getRelatedArticles(context,0,0,article,3),
                AuthorsArticles=getUserArticles(context,0,0,article.AuthorID,out count,takeCount: 3,exceptId:article.ArticleModelId),
                IsPreview=false
            };
        }

        public static List<SearchResultArticle> getRelatedArticles(ApplicationDbContext context, int order, int skip, ArticleModel article, int count)
        {
            ArticleThumbnailManager thumbnailManager = new ArticleThumbnailManager(new BlobStorageConnection());
            //直接の関係の記事を探す
            IQueryable<ArticleModel> query = context.Articles.Where(f => f.RelatedArticleId.Equals(article.ArticleModelId));
            query = ChangeOrder(order, query);
            query = query.Skip(skip);
            List<SearchResultArticle> articles = new List<SearchResultArticle>();
            foreach (var source in query.Take(count))
            {
                articles.Add(new SearchResultArticle()
                {
                    ArticleId = source.ArticleModelId,
                    LabelCount = source.LabelCount,
                    PageView = source.PageView,
                    Title = source.Title,
                    Article_UpDate = source.UpdateTime.ToShortDateString(),
                    ThumbnailTag = thumbnailManager.GenerateThumnailTag(source.ArticleModelId)
                });
            }
            int remain = count - article.LabelCount;
            if(article.LabelCount<count)
            {//取り出すカウントに満たない場合は間接的関連記事でうめる
                context.Articles.Where(f => f.ThemeId.Equals(article.ThemeId)&&!f.RelatedArticleId.Equals(article.ArticleModelId));
                query = ChangeOrder(order, query);
                query = query.Skip(skip);
                foreach (var source in query.Take(remain))
                {
                    articles.Add(new SearchResultArticle()
                    {
                        ArticleId = source.ArticleModelId,
                        LabelCount = source.LabelCount,
                        PageView = source.PageView,
                        Title = source.Title,
                        Article_UpDate = source.UpdateTime.ToShortDateString(),
                        ThumbnailTag = thumbnailManager.GenerateThumnailTag(source.ArticleModelId)
                    });
                }
            }
            return articles;
        }

        private async Task<IEnumerable<TagViewModel>> getArticleTagModels(ArticleModel article)
        {
            ApplicationDbContext context = HttpContext.GetOwinContext().Get<ApplicationDbContext>();
            int tagCount = article.Tags.Count;
            var tagVms = new TagViewModel[tagCount];
            int count = 0;
            foreach (var tagRef in article.Tags)
            {
                await context.Entry(tagRef).Collection(c => c.Articles).LoadAsync();
                tagVms[count] = new TagViewModel()
                {
                    ArticleCount = tagRef.Articles.Count,
                    TagId = tagRef.ArticleTagModelId,
                    TagName = tagRef.TagName,
                    IsTheme = tagRef.IsThemeTag
                };
                count++;
            }
            return tagVms;
        }

        // GET: Home
        public async Task<ActionResult> Index(string id)
        {
            if (String.IsNullOrWhiteSpace(id))
            {
                return View("Top",await TopViewModel.GetTopViewModel(Request,Request.GetOwinContext().Get<ApplicationDbContext>(),User.IsInRole("Administrator")));
            }
            else
            {
                var vm = await getArticleViewModel(id);
                if (vm == null || vm.Content == null) return Redirect("/");
                
                return View(vm);
            }
        }

        public ActionResult Page404()
        {
            return View();
        }

        public async Task<ActionResult> Search(string searchText,int skip=0,int order=0)
        {
            if(searchText==null)return View(new SearchResultViewModel() {Articles = new SearchResultArticle[0],Order = order,Skip = skip});
            string[] queries=searchText.Split(' ');
            var context = Request.GetOwinContext().Get<ApplicationDbContext>();
            string first = queries[0];
            var result=context.Articles.Where((f) => f.Title.Contains(first)&&!f.IsDraft);
            for (int index = 1; index < Math.Min(4,queries.Length); index++)
            {
                var query = queries[index];
                result=result.Where(f => f.Title.Contains(query));
            }
            int count = await result.CountAsync();
            result = ChangeOrder(order, result);
            result = result.Skip(skip);
            SearchResultViewModel vm=new SearchResultViewModel();
            ArticleThumbnailManager thumbnailManager=new ArticleThumbnailManager(new BlobStorageConnection());
            List<SearchResultArticle> articles=new List<SearchResultArticle>();
            foreach (var source in result.Take(20))
            {
                articles.Add(new SearchResultArticle()
                {
                    ArticleId = source.ArticleModelId,
                    LabelCount = source.LabelCount,
                    PageView = source.PageView,
                    Title = source.Title,
                    Article_UpDate = source.UpdateTime.ToShortDateString(),
                    ThumbnailTag = thumbnailManager.GenerateThumnailTag(source.ArticleModelId)
                });
            }
            vm.Articles = articles.ToArray();
            if (vm.Articles.Length == 0)
            {
                vm.SearchResultText = string.Format("「{0}」に関する検索結果は見つかりませんでした。", searchText);
            }
            else
            {
                vm.SearchResultText = string.Format("「{0}」に関する検索結果:{1}件中{2}～{3}件", searchText,count,skip+1,Math.Min(skip+21,count));
            }
            vm.SearchText = searchText;
            vm.Order = order;
            vm.Skip = skip;
            vm.Count = count;
            return View(vm);
        }

        private static IQueryable<ArticleModel> ChangeOrder(int order, IQueryable<ArticleModel> result)
        {
            switch (order)
            {
                case 6:
                    result = result.OrderBy(f => f.LabelCount);
                    break;
                case 5:
                    result = result.OrderBy(f => f.PageView);
                    break;
                case 4:
                    result = result.OrderBy(f => f.CreationTime);
                    break;
                case 3:
                    result = result.OrderByDescending(f => f.LabelCount);
                    break;
                case 2:
                    result = result.OrderByDescending(f => f.PageView);
                    break;
                case 1:
                default:
                    result = result.OrderByDescending(f => f.CreationTime);
                    break;
            }
            return result;
        }

        public async Task<ActionResult> Tag(string tag, int skip = 0, int order = 0)
        {
            if (tag == null) return View("Search",new SearchResultViewModel() { Articles = new SearchResultArticle[0],Order = order,Skip = skip});
            var context = Request.GetOwinContext().Get<ApplicationDbContext>();
            ArticleTagModel tagModel = context.Tags.Where(f => f.TagName.Equals(tag)).FirstOrDefault();
            await context.Entry(tagModel).Collection(f=>f.Articles).LoadAsync();
            SearchResultViewModel vm = new SearchResultViewModel();
            List<SearchResultArticle> articles = new List<SearchResultArticle>();
            var query = tagModel.Articles.AsQueryable();
            var count = query.Count();
            query=ChangeOrder(order, query);
            query=query.Skip(skip);
            foreach (var source in query.Take(10))
            {
                articles.Add(new SearchResultArticle()
                {
                    ArticleId = source.ArticleModelId,
                    LabelCount = source.LabelCount,
                    PageView = source.PageView,
                    Title = source.Title
                });
            }
            vm.Articles = articles.ToArray();
            vm.SearchText = tag;
            vm.Order = order;
            vm.Skip = skip;
            vm.Count = count;
            if (vm.Articles.Length == 0)
            {
                vm.SearchResultText = string.Format("「{0}」タグがついている記事は見つかりませんでした。", tag);
            }
            else
            {
                vm.SearchResultText = string.Format("「{0}」タグのついている記事、検索結果{1}件中{2}～{3}件", tag, count, skip + 1, Math.Min(skip + 21, count));
            }
            return View("Search", vm);
        }

        [HttpGet]
        [Authorize]
        public  ActionResult MyPage(int order=0,int skip=0)
        {
            var context = Request.GetOwinContext().Get<ApplicationDbContext>();
            int count = 0;
            var articles = getUserArticles(context,order, skip, User.Identity.Name,out count, takeCount: 10);
            return View(new MyPageViewModel() { Count = count,Skip=skip,Order = order,articles = articles.ToArray(),IsMyPage = true});
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> DeleteArticle(string articleId)
        {
            var context = Request.GetOwinContext().Get<ApplicationDbContext>();
            ArticleModel article = await context.Articles.FindAsync(articleId);
            if (article==null||!article.AuthorID.Equals(User.Identity.Name)) return View("Page403");
            context.Entry(article).Collection(a => a.Tags);
            article.Tags.Clear();
            BlobStorageConnection connection=new BlobStorageConnection();
            ArticleBodyTableManager abtm=new ArticleBodyTableManager(connection);
            ArticleMarkupTableManager amtm=new ArticleMarkupTableManager(connection);
            await abtm.RemoveArticle(articleId);
            await amtm.RemoveArticle(articleId);
            context.Articles.Remove(article);
            await context.SaveChangesAsync();
            return RedirectToAction("MyPage");
        }

        [HttpGet]
        public async Task<ActionResult> UserPage(string articleId,int order=0,int skip=0)
        {
            ApplicationDbContext context = Request.GetOwinContext().Get<ApplicationDbContext>();
            ArticleModel articleModel = await context.Articles.FindAsync(articleId);
            string userId = articleModel.AuthorID;
            if (User.Identity.Name.Equals(userId)) return Redirect("MyPage");
            int count = 0;
            var articles = getUserArticles(context,order, skip, userId,out count);
            var user =await Request.GetOwinContext().GetUserManager<ApplicationUserManager>().FindByNameAsync(articleModel.AuthorID);
            return View("MyPage",new UserPageViewModel() { Count = count,Skip=skip,Order = order,UserNickName =user.NickName ,articles = articles.ToArray(),IsMyPage=false });
        }

        //TODO パフォーマンス改善
        public static List<SearchResultArticle> getUserArticles(ApplicationDbContext context, int order, int skip, string userId, out int count, int takeCount = 10,string exceptId="")
        {
            ArticleThumbnailManager thumbnailManager=new ArticleThumbnailManager(new BlobStorageConnection());
            IQueryable<ArticleModel> query = String.IsNullOrWhiteSpace(exceptId)
                ? context.Articles.Where(f => f.AuthorID.Equals(userId))
                : context.Articles.Where(f => f.AuthorID.Equals(userId) && !f.ArticleModelId.Equals(exceptId));
            count = query.Count();
            query = ChangeOrder(order, query);
            query = query.Skip(skip);
            List<SearchResultArticle> articles = new List<SearchResultArticle>();
            foreach (var source in query.Take(takeCount))
            {
                articles.Add(new SearchResultArticle()
                {
                    ArticleId = source.ArticleModelId,
                    LabelCount = source.LabelCount,
                    PageView = source.PageView,
                    Title = source.Title,
                    Article_UpDate = source.UpdateTime.ToShortDateString(),
                    ThumbnailTag=thumbnailManager.GenerateThumnailTag(source.ArticleModelId)
                });
            }
            return articles;
        }

        public ActionResult Outline()
        {
            return View();
        }

        public ActionResult PrivacyPolicy()
        {
            return View();
        }

        public ActionResult TermsOfService()
        {
            return View();
        }

        public async Task<ActionResult> AllTag()
        {
            var context = Request.GetOwinContext().Get<ApplicationDbContext>();
            var themes = context.Tags.Where(f =>! f.IsThemeTag).Include(f => f.Articles).OrderBy(f => f.Articles.Count).Where(f=>f.Articles.Count>0);
            return View(new AllTagsResponse() {ThemeTags = themes});
        }

        [HttpGet]
        [AllowAnonymous]
        public ActionResult Inquiry()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Inquiry(InquiryRequest req)
        {
            InquiryLogManager.LogInquiry(req.Name, req.Mail, req.Content);
            MailMessage mailMsg = new MailMessage();

            // To
            mailMsg.To.Add(new MailAddress(req.Mail, req.Name+"様"));

            // From
            mailMsg.From = new MailAddress("inquiry@意見.みんな.com", "お問い合わせセンター");

            // Subject and multipart/alternative Body
            mailMsg.Subject = req.Name + "様からのお問い合わせに対しての返信";

            string text = "この度は＜意見.みんな＞に関するお問い合わせをいただき、誠にありがとうございます。\n" +
                          "お問い合わせいただきました内容は、下記の通りです。\n" +
                          " お名前 : \n "+req.Name+" 様\n" +
                          " ----- 内容 ----- \n "+req.Content+"\n"+
                          "頂戴いたしましたお問い合わせにつきましては、内容を確認の上、後ほどご回答いたします。\n" +
                          "なお、お問い合わせの内容によっては、ご回答まで数日かかる場合やご回答いたしかねる場合がございます。\n" +
                          "恐れ入りますが、予めご了承くださいますようお願いいたします。\n" +
                          "※このメールには返信できません。\n" +
                          "恐れ入りますが、＜意見.みんな＞に関するお問い合わせは下記URLよりお願いいたします。\n" +
                          "＜意見.みんな＞:http://xn--jgu241g.xn--q9jyb4c/　\n";

            string html = @"<p>この度は＜意見.みんな＞に関するお問い合わせをいただき、誠にありがとうございます。</p>" +
                          @"<p>お問い合わせいただきました内容は、下記の通りです。</p>" +
                          @"<p> お名前 : " + req.Name + " 様</p>" +
                          @"<p> ----- 内容 ----- </p>" +
                          @"<p>" + req.Content + "</p>" +
                          @"<p>頂戴いたしましたお問い合わせにつきましては、内容を確認の上、後ほどご回答いたします。</p>" +
                          @"<p>なお、お問い合わせの内容によっては、ご回答まで数日かかる場合やご回答いたしかねる場合がございます。</p>" +
                          @"<p>恐れ入りますが、予めご了承くださいますようお願いいたします。</p>" +
                          @"<p>※このメールには返信できません。</p>" +
                          @"<p>恐れ入りますが、＜意見.みんな＞に関するお問い合わせは下記URLよりお願いいたします。</p>" +
                          @"<p>＜意見.みんな＞:http://xn--jgu241g.xn--q9jyb4c/　</p>"; ;
                          
            mailMsg.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(text, null, MediaTypeNames.Text.Plain));
            mailMsg.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(html, null, MediaTypeNames.Text.Html));
            LinophiMailClient.Instance.SendMessage(mailMsg);
            InquiryNotifyManager.SendInquiryNotification(req.Name,req.Mail,req.Content);
            return View("InquiryAccepted");
        }


        public class AllTagsResponse
        {
             public IQueryable<ArticleTagModel> ThemeTags { get; set; } 
        }

        public class InquiryRequest
        {
             public string Name { get; set; }

            public string Mail { get; set; }

            public string Content { get; set; }
        }
    }
}