mergeInto(LibraryManager.library, {
 
    GetURLFromPage: function () {
        var returnStr = window.location.href;
        console.log("JavaScript read url: " + returnStr);
        var bufferSize = lengthBytesUTF8(returnStr) + 1
        var buffer = _malloc(bufferSize);
        stringToUTF8(returnStr, buffer, bufferSize);
        return buffer;
    },

    Navigate: function (url, windowId = "_self") {
        const urlString = UTF8ToString(url);
        const windowIdString = UTF8ToString(windowId);
        console.log("Navigate to: " + urlString + " in " + windowIdString);
        if (windowIdString == "_self")
        {
            window.top.location.assign(urlString);
        }
        else 
        {
            var a = document.createElement('a');
            a.href = urlString;
            a.setAttribute('target', '_blank');
            a.click();
        }
    },

    CopyToClipboard: function(value) {
        const valueString = UTF8ToString(value);
        if (navigator.clipboard && navigator.clipboard.writeText) {
            navigator.clipboard.writeText(valueString);
            return true;
        }
        return false;
    },

    CacheValue: function (key, value) {
        const valueString = UTF8ToString(value);
        const keyString = UTF8ToString(key);
        console.log("Should cache " + valueString + " for key " + keyString);
        localStorage.setItem(keyString, valueString);
    },

    SaveAuthorizationCookie: function (value) {
        const valueString = UTF8ToString(value);
        console.log("Should write cookie portalAccessToken: " + valueString);
        document.cookie = "portalAccessToken="+valueString+"; Secure; SameSite=strict; Path=/; Domain=.unity.com; Session=true";
    },

    ClearCache: function (key) {
        const keyString = UTF8ToString(key);
        console.log("Should clear cache for key " + keyString);
        localStorage.removeItem(keyString);
    },

    RetrieveCachedValue: function (key) {
        const keyString = UTF8ToString(key);
        console.log("Should retrieve cached value for " + keyString);
        const value = localStorage.getItem(keyString);
        if (value)
        {
            var bufferSize = lengthBytesUTF8(value) + 1;
            var buffer = _malloc(bufferSize);
            stringToUTF8(value, buffer, bufferSize);
            return buffer;
        }
        return '';
    },

    GetQueryParam: function(paramId) {
        var urlParams = new URLSearchParams(location.search);
        var param = urlParams.get(UTF8ToString(paramId));
        console.log("JavaScript read param: " + param);
        var bufferSize = lengthBytesUTF8(param) + 1;
        var buffer = _malloc(bufferSize);
        stringToUTF8(param, buffer, bufferSize);
        return buffer;
    }
});