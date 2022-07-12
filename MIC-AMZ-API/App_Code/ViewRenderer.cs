using System;
using System.IO;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;

namespace MIC_AMZ_API.App_Code
{
    public class ViewRenderer
    {
        public static T CreateController<T>(RouteData routeData = null) where T : Controller, new()
        {
            var controller = new T();

            HttpContextBase wrapper;
            if (HttpContext.Current != null)
                wrapper = new HttpContextWrapper(HttpContext.Current);
            else
                throw new InvalidOperationException("Can't create Controller Context if no active HttpContext instance is available.");

            if (routeData == null)
                routeData = new RouteData();

            if (!routeData.Values.ContainsKey("controller") && !routeData.Values.ContainsKey("Controller"))
                routeData.Values.Add("controller", controller.GetType().Name.ToLower().Replace("controller", ""));

            controller.ControllerContext = new ControllerContext(wrapper, routeData, controller);
            return controller;
        }

        public static string RenderViewToString(ControllerContext context, string viewPath, object model = null, bool partial = false)
        {
            ViewEngineResult viewEngineResult = null;
            if (partial)
                viewEngineResult = ViewEngines.Engines.FindPartialView(context, viewPath);
            else
                viewEngineResult = ViewEngines.Engines.FindView(context, viewPath, null);

            if (viewEngineResult == null)
                throw new FileNotFoundException("View cannot be found.");

            var view = viewEngineResult.View;
            context.Controller.ViewData.Model = model;

            string result = null;

            using (var stringWriter = new StringWriter())
            {
                var viewContext = new ViewContext(context, view, context.Controller.ViewData, context.Controller.TempData, stringWriter);
                view.Render(viewContext, stringWriter);
                result = stringWriter.ToString();
            }

            return result.Trim();
        }
    }

    public class GenericController : Controller
    {
    }
}