﻿using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.Owin;
using Microsoft.Owin.Security;
using Microsoft.Owin.Security.DataHandler.Encoder;
using Web.Api.Article;
using Web.Models;
using Web.Storage;
using Web.Storage.Connection;

namespace Web.Controllers
{
    [Authorize]
    public class EditController : Controller
    {
        [HttpGet]
        [AllowAnonymous]
        public ActionResult RedirectToEdit()
        {
            if (User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Index");
            }
            else
            {
                return Redirect("/Account/Login?returnUrl=/RedirectToEdit");
            }
        }

        [HttpGet]
        [Authorize]
        // GET: Edit
        public async Task<ActionResult> Index(string articleId)
        {
            if(articleId==null||string.IsNullOrWhiteSpace(articleId))
            return View();
            //記事IDがある場合
            ArticleEditViewModel vm=new ArticleEditViewModel();
            //対象の記事の取得
            var context = Request.GetOwinContext().Get<ApplicationDbContext>();
            ArticleModel articleModel = await context.Articles.FindAsync(articleId);
            if (!articleModel.AuthorID.Equals(User.Identity.Name))
            {
                return View("Page403");
            }
            ArticleMarkupTableManager amtm = new ArticleMarkupTableManager(new BlobStorageConnection());
            string markup = await amtm.GetArticleBodyAsync(articleId);
            vm.MarkupString = markup;
            return View(vm);
        }

        [Authorize]
        [ValidateAntiForgeryToken]
        [HttpPost]
        public async Task<ActionResult> Index(EditViewModel vm)
        {
            ApplicationDbContext context = HttpContext.GetOwinContext().Get<ApplicationDbContext>();
            if (!ArticleController.IsValidTitle(vm.Title, context).IsOK) return RedirectToAction("Page404", "Home");
            var article = ArticleModel.GenerateArticle(vm.Title,User.Identity.Name);
            //タグの処理
            var tags = System.Web.Helpers.Json.Decode<string[]>(vm.Tag);
            if (tags.Length > 5) return RedirectToAction("Page404","Home");
            foreach (var tag in tags)
            {
                ArticleTagModel tagModel = context.Tags.Where(f => f.TagName.Equals(tag)).SingleOrDefault();
                bool needToAdd = tagModel == null;
                tagModel = tagModel ?? ArticleTagModel.GenerateTag(tag);
                if (needToAdd)
                {
                    context.Tags.Add(tagModel);
                }
                else
                {
                    context.Entry(tagModel).Collection(c=>c.Articles).Load();
                }
                tagModel.Articles.Add(article);
                article.Tags.Add(tagModel);
            }
            //記事の追加
            context.Articles.Add(article);
            context.SaveChanges();
            var connection = new BlobStorageConnection();
            ArticleBodyTableManager abtm=new ArticleBodyTableManager(connection);
            ArticleMarkupTableManager amtm=new ArticleMarkupTableManager(connection);
            await abtm.AddArticle(article.ArticleModelId,vm.Body);
            await amtm.AddMarkupAsync(article.ArticleModelId, vm.Markup);
            return Redirect("~/"+article.ArticleModelId);
        }
    }
}