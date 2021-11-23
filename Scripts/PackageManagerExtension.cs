#pragma warning disable IDE0051 // Remove unused private members
#pragma warning disable IDE0161 // Convert to file-scoped namespace

using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEditor.PackageManager.UI;
using UnityEngine;
using UnityEngine.UIElements;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace AdvancedSceneManager.Plugin.PackageManager
{

    static class Extensions
    {

        public static bool IsDependency(this (string packageName, string displayName, string uri) package) =>
           !string.IsNullOrEmpty(package.uri);

        public static bool IsPlugin(this (string packageName, string displayName, string uri) package) =>
            package.packageName.StartsWith("plugin");

        public static bool IsExample(this (string packageName, string displayName, string uri) package) =>
            package.packageName.StartsWith("example");

        public static string ButtonText(this (string packageName, string displayName, string uri) package, bool isInstalled = false)
        {
            if (package.IsDependency() || package.IsPlugin())
                return isInstalled ? "Remove from project" : "Import into project";
            else if (package.IsExample())
                return "View on github";
            return null;
        }

        public static string GetUri(this (string packageName, string displayName, string uri) package)
        {
            if (package.IsExample())
                return $"https://github.com/Lazy-Solutions/{package.packageName}.git";
            else if (package.IsPlugin())
                return $"https://github.com/Lazy-Solutions/{package.packageName}.git#asm-{ASM.version}";
            else if (package.IsDependency())
                return package.uri;
            return null;
        }

    }

    class PackageManagerExtension : IPackageManagerExtension
    {

        static readonly (string packageName, string displayName, string uri)[] packages =
        {

            //Plugins
            ("plugin.asm.addressables",     "Addressables", ""),
            ("plugin.asm.locking",          "Lock collections and scenes", ""),

            //Examples
            ("example.asm.level-select",    "Level select", ""),
            ("example.asm.preloading",      "Preloading", ""),
            ("example.asm.streaming",       "Streaming", ""),

            //Dependencies
            ("com.unity.editorcoroutines",  "Editor Coroutines", "1.0.0" ),
            ("utility.lazy.coroutines",     "Lazy.CoroutineUtility", "https://github.com/Lazy-Solutions/Unity.CoroutineUtility.git#asm"),

        };

        const string currentVersionFile = "Packages/plugin.asm.package-manager/package.json";

        [InitializeOnLoadMethod]
        static void OnLoad()
        {
            PackageManagerExtensions.RegisterExtension(new PackageManagerExtension());
            SetVersion();
        }

        #region ASM version

        //Sets version in package.json, since this is what we use to display to the user what version asm is
        static void SetVersion()
        {

            var json = File.ReadAllText(currentVersionFile);
            var originalFile = json;

            var match = Regex.Match(json, ".*\"version\": \"(.*)\",");
            var version = Version.Parse(match.Groups[1].Value).ToString();

            json = json.Replace(match.Groups[0].Value, match.Groups[0].Value.Replace(version.ToString(), GetVersion()));
            if (json != originalFile)
                File.WriteAllText(currentVersionFile, json);

        }

        static string GetVersion()
        {

            const string versionResource = "AdvancedSceneManager/version";

            return
                Resources.Load<TextAsset>(versionResource) is TextAsset t && t
                ? t.text
                : "1.3.2";

        }

        #endregion
        #region UI

        VisualElement element;
        public VisualElement CreateExtensionUI() =>
            Refresh();

        VisualElement Refresh()
        {

            if (element == null)
                element = new VisualElement() { name = "asm" };

            element.Clear();

            var list = new VisualElement();
            element.Add(list);

            element.style.marginLeft = 6;
            list.style.marginTop = 6;

            var linkContainer = new VisualElement();
            linkContainer.style.flexDirection = FlexDirection.Row;
            linkContainer.style.marginLeft = -6;
            linkContainer.style.marginTop = -6;
            linkContainer.style.marginBottom = 6;

            linkContainer.Add(CreateLink("View documentation", "https://github.com/Lazy-Solutions/advanced-scene-manager/wiki"));
            linkContainer.Add(CreateLink("View changelog", "https://assetstore.unity.com/packages/tools/utilities/advanced-scene-manager-174152#releases"));
            list.Add(linkContainer);

            CreateSection("Dependencies", packages.Where(Extensions.IsDependency).ToArray(), list);

            CreateSection("Plugins", packages.Where(Extensions.IsPlugin).ToArray(), list);
            CreateSection("Samples", packages.Where(Extensions.IsExample).ToArray(), list);

            return element;

        }

        VisualElement CreateLink(string text, string uri)
        {

            var link = new Button() { text = text };
            link.EnableInClassList("unity-button", true);
            link.EnableInClassList("link", true);

            link.clicked += () => Application.OpenURL(uri);

            return link;

        }

        void CreateSection(string header, (string packageName, string displayName, string uri)[] packages, VisualElement list)
        {

            var text = new TextElement() { text = header };
            text.AddToClassList("containerTitle");
            list.Add(text);
            text.style.marginTop = 6;

            foreach (var package in packages)
                list.Add(CreateElement(package));

        }

        VisualElement CreateElement((string packageName, string displayName, string uri) package)
        {

            var element = new VisualElement();
            var text = new Label() { text = package.displayName };

            element.Add(text);

            element.style.marginTop = 6;
            element.style.height = 22;
            element.style.flexDirection = FlexDirection.Row;

            text.style.width = 200;
            text.tooltip = package.displayName + "\n" + package.packageName;

            if (package.IsDependency() && IsDependency(package.packageName))
            {
                var check = new TextElement() { text = "✓" };
                check.style.color = Color.green;
                element.Add(check);
            }
            else
            {
                var button = new Button(OnClick)
                { text = package.ButtonText() };
                element.Add(button);
            }

            return element;

            void OnClick()
            {

                var uri = package.GetUri();

                if (package.IsDependency())
                    SetDependency(package.packageName, uri);
                else if (package.IsPlugin())
                    SetDependency(package.packageName, uri, !IsDependency(package.packageName));
                else if (package.IsExample())
                    Application.OpenURL(uri);

            }

        }

        static bool IsDependency(string packageName)
        {
            var manifestFile = "Packages/manifest.json";
            var file = File.ReadAllText(manifestFile);
            return file.Contains(packageName);
        }

        static void SetDependency(string packageName, string uri, bool isSet = true)
        {

            var manifestFile = "Packages/manifest.json";
            var file = File.ReadAllText(manifestFile);
            var originalFile = file;

            var str = Environment.NewLine + $"\t\t\"{packageName}\": \"{uri}\",";

            if (isSet && !file.Contains(packageName))
                file = file.Insert(file.LastIndexOf(",") + 1, str);
            else if (!isSet && file.Contains(str))
                file = file.Replace(str, "");

            if (file != originalFile)
            {
                File.WriteAllText(manifestFile, file);
                CompilationPipeline.RequestScriptCompilation();
                //EditorUtility.DisplayProgressBar("Unity Package Manager", "Resolving packages...", 0);
            }

        }

        public void OnPackageAddedOrUpdated(PackageInfo packageInfo) =>
            Refresh();

        public void OnPackageRemoved(PackageInfo packageInfo) =>
            Refresh();

        public void OnPackageSelectionChange(PackageInfo packageInfo)
        {
            if (element != null)
            {

                var isOurs = ASM.assemblyNames.Contains(packageInfo?.name);

                element.style.display = isOurs ? DisplayStyle.Flex : DisplayStyle.None;
                if (element.parent.parent.Q("detailLinksContainer") is VisualElement linksSection)
                    linksSection.style.display = isOurs ? DisplayStyle.None : DisplayStyle.Flex;

            }
        }

        #endregion

    }

}
