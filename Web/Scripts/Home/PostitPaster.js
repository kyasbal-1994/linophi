﻿$(window).load(function () {
    var htmlHeight = $('.foot').offset().top + $('.foot').outerHeight();

    $('html').css({
        "height": htmlHeight + "px"
    });

    var postitJson = JSON.parse($('#label-info').text());
    console.log(postitJson);

    var articleId = location.pathname.substr(1);

    /*
    * ふせんをクリックされたら左側に影的なものを出して周りを暗くする
    * 次のタイミングにクリックされたら貼り付ける
    * 貼り付けられた（クリック領域でクリックされたら）そこに貼る
    * それはこっちで用意した場所に追加されるが、すでに貼られていた場合は値を増やす。
    */
    var pasteMode = false;

    var labelType, src;
    var dropboxPos = $('.contentswrapper').offset().top, dropboxHeight = $('.contentswrapper').outerHeight(true);

    var posY = dropboxPos + 10;

    $('.article-container > *').each(function (i) {
        var $ele = $('[class^="x_p-"]:nth-child(' + (i + 1) + ')');

        var className = $ele.attr("class");

        // alert(className);
        var eleHeight = $ele.outerHeight(true), elePos = $ele.offset().top;

        $('.dropbox').append('<div class="' + className + '"></div>');

        $('.dropbox > .' + className).css({
            "position": "absolute",
            "top": elePos - dropboxPos + "px",
            "height": eleHeight + "px",
            "width": "300px"
        });
        for (var j = 0, len = postitJson.length; j < len; j++) {
            if (postitJson[j]["ParagraphId"] == className.substr(4)) {
                var data = JSON.parse(postitJson[j]["Data"]);
                data = _.sortBy(data, function (d) {
                    return (Object)(d).Value;
                }).reverse();

                for (var i = 0; i < data.length; i++) {
                    $('.dropbox > .' + className).append('<div class="' + data[i].Key + '" style="background-image:url(\'/Content/imgs/Home/' + data[i].Key + '.png\');background-size:130px 43px;height:43px;width:130px;"><span>' + data[i].Value + '</span></div>');
                }
            }
        }
    });

    // 貼り付けモードへ
    $('.postit-list [class]').click(function (event) {
        pasteMode = true;

        $('.fade-layer').css({
            "visibility": "visible",
            "opacity": 1
        });

        labelType = ((Object)(event.currentTarget)).className;
        src = event.currentTarget.src; // なぜかVSで赤線がでるけどちゃんと動きます

        $('.fade-layer, .dropbox').mousemove(function (e) {
            if (dropboxPos <= e.pageY && e.pageY <= dropboxPos + dropboxHeight) {
                posY = e.pageY;
            }

            if (pasteMode) {
                $('.dropbox').css({
                    "opacity": 0.7,
                    "z-index": 1100
                });
                $(".dropbox > .postit-pasting").css({
                    "position": "absolute",
                    "top": posY - dropboxPos + "px",
                    "left": "20px",
                    "z-index": 1100,
                    "visibility": "visible",
                    "background-image": "url(" + src + ")",
                    "background-size": "130px 43px"
                });
            }
            var pHeights = dropboxPos;

            $('.dropbox > [class^="x_p-"]').each(function (i) {
                var $target = $('.dropbox > [class^="x_p-"]:nth-child(' + (i + 2) + ')');
                var pHeight = $target.outerHeight(true);
                var bg = "none";
                if (pHeights <= posY && posY <= pHeights + pHeight && pasteMode)
                    bg = "#fcc";

                $target.css({
                    "background": bg
                });

                pHeights += pHeight;
            });
        });

        console.log("called", labelType);
    });

    // 貼り付けて戻る
    $('.dropbox').click(function () {
        $('.fade-layer').css("opacity", 0);
        setTimeout(function () {
            $('.fade-layer').css("visibility", "hidden");
        }, 500);

        $('.dropbox').css("opacity", 1);
        setTimeout(function () {
            $('.dropbox').css("z-index", 0);
        }, 500);

        $('.dropbox > .postit-pasting').css({
            "z-index": -100,
            "visibility": "hidden"
        });

        if (pasteMode) {
            var pHeights = dropboxPos;

            $('.dropbox > [class^="x_p-"]').each(function (i) {
                var $target = $('.dropbox > [class^="x_p-"]:nth-child(' + (i + 1) + ')');
                var pHeight = $target.outerHeight(true);

                var thisClass = $target.attr("class");

                var postitExistence = $('.dropbox > [class^="x_p-"]:nth-child(' + (i + 1) + ') > .' + labelType).length;

                if (pHeights <= posY && posY <= pHeights + pHeight) {
                    if (postitExistence) {
                        for (var j = 0, len = postitJson.length; j < len; j++) {
                            if (postitJson[j]["ParagraphId"] == thisClass.substr(4)) {
                                $('.dropbox > .' + thisClass + ' > .' + labelType + ' > span').html(String(Number($('.dropbox > .' + thisClass + ' > .' + labelType + ' > span').text()) + 1));
                                console.info(JSON.parse(postitJson[j]["Data"])[labelType]);
                            }
                        }
                    } else {
                        $target.append('<div class="' + labelType + '" style="background-image:url(' + src + ');background-size:130px 43px;height:43px;width:130px;"><span>1</span></div>');
                    }

                    $.ajax({
                        type: "post",
                        url: "api/Label/AttachLabel",
                        data: {
                            "ArticleId": articleId,
                            "ParagraphId": thisClass.substr(4),
                            "LabelType": labelType,
                            "DebugMode": true
                        },
                        success: function (data) {
                            console.log(data);
                        }
                    });
                }

                pHeights += pHeight;
                // console.log($target.attr("class"), pHeight, pHeights, posY, src);
            });

            pasteMode = false;
        }
    });

    // 貼り付けないで戻る
    $('.fade-layer').click(function () {
        $('.fade-layer').css("opacity", 0);
        setTimeout(function () {
            $('.fade-layer').css("visibility", "hidden");
        }, 500);

        $('.dropbox').css("opacity", 1);
        setTimeout(function () {
            $('.dropbox').css("z-index", 0);
        }, 500);

        $('.dropbox > .postit-pasting').css({
            "z-index": -100,
            "visibility": "hidden"
        });

        pasteMode = false;
    });

    // ふせんを貼る部分をhoverした時の処理
    $('.postit-list [class]').hover(function (event) {
        var thisClass = ((Object)(event.currentTarget)).className;
        $('.postit-list div.' + thisClass).css("visibility", "visible");
    }, function (event) {
        var thisClass = ((Object)(event.currentTarget)).className;
        $('.postit-list div.' + thisClass).css("visibility", "hidden");
    });
});
//# sourceMappingURL=PostitPaster.js.map
