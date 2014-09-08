﻿var CommentSourceParser = (function () {
    function CommentSourceParser() {
        this.commentJson = JSON.parse($("#comment-info").text());
    }
    CommentSourceParser.prototype.eachDoInComments = function (paragraphId, handler) {
        for (var i = 0, len = this.commentJson.length; i < len; i++) {
            if (this.commentJson[i]["ParagraphId"] == paragraphId) {
                handler(this.commentJson[i]["Name"], this.commentJson[i]["PostTime"], this.commentJson[i]["AutoId"], this.commentJson[i]["Comment"]);
            }
        }
    };
    return CommentSourceParser;
})();

$(function () {
    var commentJson = JSON.parse($("#comment-info").text());

    var commentSourceParser = new CommentSourceParser();

    $('.article-container > *').each(function (i) {
        var $ele = $('[class^="x_p-"]:nth-child(' + (i + 1) + ')');
        var className = $ele.attr("class");

        var thisHtml = "";

        commentSourceParser.eachDoInComments(className.substr(4), function (name, time, id, comment) {
            thisHtml += '<div class="response">' + '<p class="res-title"> counter <b>' + name + '</b> <small>[' + time + '] ID:' + id + ' </small> </p>' + '<p class="res-text">' + comment + '</p>' + '</div>';
        });

        if (thisHtml) {
            $('.widget .' + className).append('<div class="' + className + '-comments">' + thisHtml + '</div>');
        }
    });
});
//# sourceMappingURL=CommentIndicator.js.map
