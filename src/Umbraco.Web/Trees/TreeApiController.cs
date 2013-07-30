﻿using System;
using System.Globalization;
using System.Linq;
using System.Net.Http.Formatting;
using Umbraco.Core;
using Umbraco.Web.WebApi;
using Umbraco.Web.WebApi.Filters;

namespace Umbraco.Web.Trees
{
    /// <summary>
    /// The base controller for all tree requests
    /// </summary>
    public abstract class TreeApiController : UmbracoAuthorizedApiController
    {
        private readonly TreeAttribute _attribute;

        /// <summary>
        /// Remove the xml formatter... only support JSON!
        /// </summary>
        /// <param name="controllerContext"></param>
        protected override void Initialize(global::System.Web.Http.Controllers.HttpControllerContext controllerContext)
        {
            base.Initialize(controllerContext);
            controllerContext.Configuration.Formatters.Remove(controllerContext.Configuration.Formatters.XmlFormatter);
        }

        protected TreeApiController()
        {           
            //Locate the tree attribute
            var treeAttributes = GetType()
                .GetCustomAttributes(typeof (TreeAttribute), false)
                .OfType<TreeAttribute>()
                .ToArray();
            
            if (treeAttributes.Any() == false)
            {
                throw new InvalidOperationException("The Tree controller is missing the " + typeof(TreeAttribute).FullName + " attribute");
            }

            //assign the properties of this object to those of the metadata attribute
            _attribute = treeAttributes.First();            
        }

        /// <summary>
        /// The method called to render the contents of the tree structure
        /// </summary>
        /// <param name="id"></param>
        /// <param name="queryStrings">
        /// All of the query string parameters passed from jsTree
        /// </param>
        /// <remarks>
        /// We are allowing an arbitrary number of query strings to be pased in so that developers are able to persist custom data from the front-end
        /// to the back end to be used in the query for model data.
        /// </remarks>
        protected abstract TreeNodeCollection GetTreeData(string id, FormDataCollection queryStrings);

        /// <summary>
        /// Returns the menu structure for the node
        /// </summary>
        /// <param name="id"></param>
        /// <param name="queryStrings"></param>
        /// <returns></returns>
        protected abstract MenuItemCollection GetMenuForNode(string id, FormDataCollection queryStrings);
        
        /// <summary>
        /// The name to display on the root node
        /// </summary>
        public virtual string RootNodeDisplayName
        {
            get { return _attribute.Title; }
        }

        /// <summary>
        /// Returns the root node for the tree
        /// </summary>
        /// <param name="queryStrings"></param>
        /// <returns></returns>
        [HttpQueryStringFilter("queryStrings")]
        public TreeNode GetRootNode(FormDataCollection queryStrings)
        {
            if (queryStrings == null) queryStrings = new FormDataCollection("");
            return CreateRootNode(queryStrings);
        }

        /// <summary>
        /// The action called to render the contents of the tree structure
        /// </summary>
        /// <param name="id"></param>
        /// <param name="queryStrings">
        /// All of the query string parameters passed from jsTree
        /// </param>
        /// <returns>JSON markup for jsTree</returns>        
        /// <remarks>
        /// We are allowing an arbitrary number of query strings to be pased in so that developers are able to persist custom data from the front-end
        /// to the back end to be used in the query for model data.
        /// </remarks>
        [HttpQueryStringFilter("queryStrings")]
        public TreeNodeCollection GetNodes(string id, FormDataCollection queryStrings)
        {
            if (queryStrings == null) queryStrings = new FormDataCollection("");
            return GetTreeData(id, queryStrings);
        }

        /// <summary>
        /// The action called to render the menu for a tree node
        /// </summary>
        /// <param name="id"></param>
        /// <param name="queryStrings"></param>
        /// <returns></returns>
        [HttpQueryStringFilter("queryStrings")]
        public MenuItemCollection GetMenu(string id, FormDataCollection queryStrings)
        {
            if (queryStrings == null) queryStrings = new FormDataCollection("");
            return GetMenuForNode(id, queryStrings);
        }

        /// <summary>
        /// Helper method to create a root model for a tree
        /// </summary>
        /// <returns></returns>
        protected virtual TreeNode CreateRootNode(FormDataCollection queryStrings)
        {
            var rootNodeAsString = Constants.System.Root.ToString(CultureInfo.InvariantCulture);

            var getChildNodesUrl = Url.GetTreeUrl(
                GetType(),
                rootNodeAsString, 
                queryStrings);

            var getMenuUrl = Url.GetMenuUrl(
                GetType(),
                rootNodeAsString,
                queryStrings);

            var isDialog = queryStrings.GetValue<bool>(TreeQueryStringParameters.DialogMode);

            //var node = new TreeNode(RootNodeId, BackOfficeRequestContext.RegisteredComponents.MenuItems, jsonUrl)
            var node = new TreeNode(
                rootNodeAsString, 
                getChildNodesUrl,
                getMenuUrl)
                {
                    HasChildren = true,

                    ////THIS IS TEMPORARY UNTIL WE FIGURE OUT HOW WE ARE LOADING STUFF (I.E. VIEW NAMES, ACTION NAMES, DUNNO)
                    //EditorUrl = queryStrings.HasKey(TreeQueryStringParameters.OnNodeClick) //has a node click handler?
                    //                ? queryStrings.Get(TreeQueryStringParameters.OnNodeClick) //return node click handler
                    //                : isDialog //is in dialog mode without a click handler ?
                    //                      ? "#" //return empty string, otherwise, return an editor URL:
                    //                      : "mydashboard", 

                    Title = RootNodeDisplayName
                };

            //add the tree type to the root
            node.AdditionalData.Add("treeType", GetType().FullName);
            
            ////add the tree-root css class
            //node.Style.AddCustom("tree-root");

            //node.AdditionalData.Add("id", node.HiveId.ToString());
            //node.AdditionalData.Add("title", node.Title);

            AddQueryStringsToAdditionalData(node, queryStrings);

            //check if the tree is searchable and add that to the meta data as well
            if (this is ISearchableTree)
            {
                node.AdditionalData.Add("searchable", "true");
            }

            return node;
        }

        /// <summary>
        /// The AdditionalData of a node is always populated with the query string data, this method performs this
        /// operation and ensures that special values are not inserted or that duplicate keys are not added.
        /// </summary>
        /// <param name="node"></param>
        /// <param name="queryStrings"></param>
        protected virtual void AddQueryStringsToAdditionalData(TreeNode node, FormDataCollection queryStrings)
        {
            // Add additional data, ensure treeId isn't added as we've already done that
            foreach (var q in queryStrings
                .Where(x => x.Key != "treeId" && node.AdditionalData.ContainsKey(x.Key) == false))
            {
                node.AdditionalData.Add(q.Key, q.Value);
            }
        }
        
        #region Create TreeNode methods

        /// <summary>
        /// Helper method to create tree nodes
        /// </summary>
        /// <param name="id"></param>
        /// <param name="queryStrings"></param>
        /// <param name="title"></param>
        /// <returns></returns>
        public TreeNode CreateTreeNode(string id, FormDataCollection queryStrings, string title)
        {
            var jsonUrl = Url.GetTreeUrl(GetType(), id, queryStrings);
            var menuUrl = Url.GetMenuUrl(GetType(), id, queryStrings);
            var node = new TreeNode(id, jsonUrl, menuUrl) { Title = title };
            return node;
        }

        /// <summary>
        /// Helper method to create tree nodes
        /// </summary>
        /// <param name="id"></param>
        /// <param name="queryStrings"></param>
        /// <param name="title"></param>
        /// <param name="icon"></param>
        /// <returns></returns>
        public TreeNode CreateTreeNode(string id, FormDataCollection queryStrings, string title, string icon)
        {
            var jsonUrl = Url.GetTreeUrl(GetType(), id, queryStrings);
            var menuUrl = Url.GetMenuUrl(GetType(), id, queryStrings);
            var node = new TreeNode(id, jsonUrl, menuUrl) { Title = title, Icon = icon };
            return node;
        }
        
        /// <summary>
        /// Helper method to create tree nodes
        /// </summary>
        /// <param name="id"></param>
        /// <param name="queryStrings"></param>
        /// <param name="title"></param>
        /// <param name="routePath"></param>
        /// <param name="icon"></param>
        /// <returns></returns>
        public TreeNode CreateTreeNode(string id, FormDataCollection queryStrings, string title, string icon, string routePath)
        {
            var jsonUrl = Url.GetTreeUrl(GetType(), id, queryStrings);
            var menuUrl = Url.GetMenuUrl(GetType(), id, queryStrings);            
            var node = new TreeNode(id, jsonUrl, menuUrl) { Title = title, RoutePath = routePath, Icon = icon };
            return node;
        }

        /// <summary>
        /// Helper method to create tree nodes and automatically generate the json url
        /// </summary>
        /// <param name="id"></param>
        /// <param name="queryStrings"></param>
        /// <param name="title"></param>
        /// <param name="icon"></param>
        /// <param name="hasChildren"></param>
        /// <returns></returns>
        public TreeNode CreateTreeNode(string id, FormDataCollection queryStrings, string title, string icon, bool hasChildren)
        {
            var treeNode = CreateTreeNode(id, queryStrings, title, icon);
            treeNode.HasChildren = hasChildren;
            return treeNode;
        }

        /// <summary>
        /// Helper method to create tree nodes and automatically generate the json url
        /// </summary>
        /// <param name="id"></param>
        /// <param name="queryStrings"></param>
        /// <param name="title"></param>
        /// <param name="routePath"></param>
        /// <param name="hasChildren"></param>
        /// <param name="icon"></param>
        /// <returns></returns>
        public TreeNode CreateTreeNode(string id, FormDataCollection queryStrings, string title, string icon, bool hasChildren, string routePath)
        {
            var treeNode = CreateTreeNode(id, queryStrings, title, icon);
            treeNode.HasChildren = hasChildren;
            treeNode.RoutePath = routePath;
            return treeNode;
        }

        #endregion

        /// <summary>
        /// The tree name based on the controller type so that everything is based on naming conventions
        /// </summary>
        public string TreeType
        {
            get
            {
                var name = GetType().Name;
                return name.Substring(0, name.LastIndexOf("TreeController", StringComparison.Ordinal));
            }
        }
       
        
    }
}
