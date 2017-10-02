var chakram = require("chakram");
var expect = require("chakram").expect;

var webdriver = require("selenium-webdriver"),
    By = webdriver.By,
    until = webdriver.until;

describe("authorization tests", function () {
    var baseAuthUrl = process.env.BASE_AUTH_URL;
    var baseIdentityUrl = process.env.BASE_IDENTITY_URL;
    var fabricInstallerSecret = process.env.FABRIC_INSTALLER_SECRET;
    console.log("fabric installer secret:" + fabricInstallerSecret);

    if (!baseAuthUrl) {
        baseAuthUrl = "http://localhost:5004";
    }

    if (!baseIdentityUrl) {
        baseIdentityUrl = "http://localhost:5001";
    }

    var newClientSecret = "";
    var newAuthClientAccessToken = "";

    var authRequestOptions = {
        headers: {
            "Content-Type": "application/json",
            "Accept": "application/json",
            "Authorization": ""
        }
    }
    var requestOptions = {
        headers: {
            "content-type": "application/json"
        }
    }

    var identityClientFuncTest = {
        "clientId": "func-test",
        "clientName": "Functional Test Client",
        "requireConsent": "false",
        "allowedGrantTypes": ["client_credentials", "password"],
        "allowedScopes": [
            "fabric/identity.manageresources",
            "fabric/authorization.read",
            "fabric/authorization.write",
            "openid",
            "profile"
        ]
    }

    var authClientFuncTest = {
        "id": "func-test",
        "name": "Functional Test Client",
        "topLevelSecurableItem": { "name": "func-test" }
    }

    var groupFoo = {
        "id": "FABRIC\\\Health Catalyst Viewer",
        "groupName": "FABRIC\\\Health Catalyst Viewer",
        "groupSource": "Active Directory"
    }

    var groupBar = {
        "id": "FABRIC\\\Health Catalyst Editor",
        "groupName": "FABRIC\\\Health Catalyst Editor",
        "groupSource": "Custom"
    }

    var userBar = {
        "subjectId": "first.last@gmail.com",
        "identityProvider": "Windows"
    }

    var roleFoo = {
        "Grain": "app",
        "SecurableItem": "func-test",
        "Name": "FABRIC\\\Health Catalyst Viewer"
    }

    var roleBar = {
        "Grain": "app",
        "SecurableItem": "func-test",
        "Name": "FABRIC\\\Health Catalyst Editor"
    }

    var userCanViewPermission = {
        "Grain": "app",
        "SecurableItem": "func-test",
        "Name": "userCanView"
    }

    var userCanEditPermission = {
        "Grain": "app",
        "SecurableItem": "func-test",
        "Name": "userCanEdit"
    }

    function getAccessToken(clientData) {
        return chakram.post(baseIdentityUrl + "/connect/token", undefined, clientData)
            .then(function (postResponse) {
                var accessToken = "Bearer " + postResponse.body.access_token;
                return accessToken;
            });
    }

    function getAccessTokenForInstaller(installerClientSecret) {
        var postData = {
            form: {
                "client_id": "fabric-installer",
                "client_secret": installerClientSecret,
                "grant_type": "client_credentials",
                "scope": "fabric/identity.manageresources fabric/authorization.read fabric/authorization.write fabric/authorization.manageclients fabric/identity.read"
            }
        };

        return getAccessToken(postData);
    }

    function getAccessTokenForAuthClient(newAuthClientSecret) {
        var clientData = {
            form: {
                "client_id": "func-test",
                "client_secret": newAuthClientSecret,
                "grant_type": "client_credentials",
                "scope": "fabric/authorization.read fabric/authorization.write"
            }
        }

        return getAccessToken(clientData);
    }

    function bootstrapIdentityServer() {
        return getAccessTokenForInstaller(fabricInstallerSecret)
            .then(function (retrievedAccessToken) {
                console.log("token = " + retrievedAccessToken);
                authRequestOptions.headers.Authorization = retrievedAccessToken;
            });
    }

    before("running before", function () {
        this.timeout(5000);
        return bootstrapIdentityServer();
    });

    describe("register client", function () {

        it("should register a client", function () {
            this.timeout(4000);
            return chakram.post(baseIdentityUrl + "/api/client", identityClientFuncTest, authRequestOptions)
                .then(function (clientResponse) {
                    expect(clientResponse).to.have.status(201);
                    newClientSecret = clientResponse.body.clientSecret;
                    return getAccessTokenForAuthClient(clientResponse.body.clientSecret);
                })
                .then(function (authClientAccessToken) {
                    newAuthClientAccessToken = authClientAccessToken;
                })
                .then(function () {
                    return chakram.post(baseAuthUrl + "/clients", authClientFuncTest, authRequestOptions);
                })
                .then(function (clientResponse) {
                    expect(clientResponse).to.have.status(201);
                });
        });
    });

    describe("register groups", function () {
        it("should register group foo", function () {
            var registerGroupFooResponse = chakram.post(baseAuthUrl + "/groups", groupFoo, authRequestOptions);
            return expect(registerGroupFooResponse).to.have.status(201);
        });

        it("should register group bar", function () {
            var registerGroupBarResponse = chakram.post(baseAuthUrl + "/groups", groupBar, authRequestOptions);
            return expect(registerGroupBarResponse).to.have.status(201);
        });
    });

    describe("register roles", function () {
        it("should register role foo", function () {
            authRequestOptions.headers.Authorization = newAuthClientAccessToken;

            var registerRoleFooResponse = chakram.post(baseAuthUrl + "/roles", roleFoo, authRequestOptions);
            return expect(registerRoleFooResponse).to.have.status(201);
        });

        it("should register role bar", function () {
            authRequestOptions.headers.Authorization = newAuthClientAccessToken;

            var registerRoleBarResponse = chakram.post(baseAuthUrl + "/roles", roleBar, authRequestOptions);
            return expect(registerRoleBarResponse).to.have.status(201);
        });
    });

    describe("register permissions", function () {
        it("should register permission userCanView", function () {
            authRequestOptions.headers.Authorization = newAuthClientAccessToken;

            var registerPermissionResponse = chakram.post(baseAuthUrl + "/Permissions", userCanViewPermission, authRequestOptions);
            return expect(registerPermissionResponse).to.have.status(201);
        });

        it("should register permission userCanEdit", function () {
            authRequestOptions.headers.Authorization = newAuthClientAccessToken;

            var registerPermissionResponse = chakram.post(baseAuthUrl + "/Permissions", userCanEditPermission, authRequestOptions);
            return expect(registerPermissionResponse).to.have.status(201);
        });
    });

    describe("associate groups to roles", function () {
        it("should associate group foo with role foo", function () {
            authRequestOptions.headers.Authorization = newAuthClientAccessToken;

            return chakram.get(baseAuthUrl + "/roles/" + roleFoo.Grain + "/" + roleFoo.SecurableItem + "/" + encodeURIComponent(roleFoo.Name), authRequestOptions)
                .then(function (getResponse) {
                    expect(getResponse).to.have.status(200);
                    expect(getResponse).to.comprise.of.json([{ name: "FABRIC\\Health Catalyst Viewer" }]);

                    return getResponse.body;
                })
                .then(function (role) {
                    return chakram.post(baseAuthUrl + "/groups/" + encodeURIComponent(groupFoo.groupName) + "/roles", role[0], authRequestOptions);
                })
                .then(function (postResponse) {
                    expect(postResponse).to.have.status(201);
                });
        });

        it("should associate group bar with role bar", function () {
            authRequestOptions.headers.Authorization = newAuthClientAccessToken;

            return chakram.get(baseAuthUrl + "/roles/" + roleBar.Grain + "/" + roleBar.SecurableItem + "/" + encodeURIComponent(roleBar.Name), authRequestOptions)
                .then(function (getResponse) {
                    expect(getResponse).to.have.status(200);
                    expect(getResponse).to.comprise.of.json([{ name: "FABRIC\\Health Catalyst Editor" }]);
                    return getResponse.body;
                })
                .then(function (role) {
                    return chakram.post(baseAuthUrl + "/groups/" + encodeURIComponent(groupBar.groupName) + "/roles", role[0], authRequestOptions);
                })
                .then(function (postResponse) {
                    expect(postResponse).to.have.status(201);
                });
        });
    });

    describe("associate users to groups", function () {
        it("should return 400 when no subjectId provided", function () {
            authRequestOptions.headers.Authorization = newAuthClientAccessToken;

            return chakram.post(baseAuthUrl + "/groups/" + encodeURIComponent(groupBar.groupName) + "/users", { "identityProvider": "Windows" }, authRequestOptions)
                .then(function (postResponse) {
                    expect(postResponse).to.have.status(400);
                });
        });

        it("should return 400 when no identityProvider provided", function () {
            authRequestOptions.headers.Authorization = newAuthClientAccessToken;

            return chakram.post(baseAuthUrl + "/groups/" + encodeURIComponent(groupBar.groupName) + "/users", { "subjectId": "first.last@gmail.com" }, authRequestOptions)
                .then(function (postResponse) {
                    expect(postResponse).to.have.status(400);
                });
        });

        it("should return 400 when associating user with non-custom group", function () {
            authRequestOptions.headers.Authorization = newAuthClientAccessToken;

            return chakram.post(baseAuthUrl + "/groups/" + encodeURIComponent(groupFoo.groupName) + "/users", userBar, authRequestOptions)
                .then(function (postResponse) {
                    expect(postResponse).to.have.status(400);
                });
        });

        it("should associate user bar with group bar", function () {
            authRequestOptions.headers.Authorization = newAuthClientAccessToken;

            return chakram.post(baseAuthUrl + "/groups/" + encodeURIComponent(groupBar.groupName) + "/users", userBar, authRequestOptions)
                .then(function (postResponse) {
                    expect(postResponse).to.have.status(201);
                });
        });
    });

    describe("search identities", function () {
        it("should return a 404 when client_id does not exist", function () {
            this.timeout(1000000);
            var options = {
                headers: {
                    "Accept": "application/json",
                    "Authorization": newAuthClientAccessToken
                }
            }

            return chakram.get(baseAuthUrl + "/identities?client_id=blah", options)
                .then(function (getResponse) {
                    expect(getResponse).to.have.status(404);
                });
        });

        it("should return 200 and results with valid request", function () {
            this.timeout(1000000);

            function loginUser() {
                //setup custom phantomJS capability
                var phantomjsExe = require("phantomjs").path;
                var customPhantom = webdriver.Capabilities.phantomjs();
                customPhantom.set("phantomjs.binary.path", phantomjsExe);

                //build custom phantomJS driver
                var driver = new webdriver.Builder().withCapabilities(customPhantom).build();

                driver.manage().window().setSize(1024, 768);
                var encodedRedirectUri = encodeURIComponent(baseIdentityUrl);
                return driver.get(baseIdentityUrl +
                        "/account/login?returnUrl=%2Fconnect%2Fauthorize%2Flogin%3Fclient_id%3Dfunc-test%26redirect_uri%3D" +
                        encodedRedirectUri +
                        "%26response_type%3Did_token%2520token%26scope%3Dopenid%2520profile%2520fabric%252Fauthorization.read%2520fabric%252Ffabric%252Fauthorization.write%26nonce%3Dd9bfc7af239b4e99b18cb08f69f77377")
                    .then(function () {

                        //sign in using driver
                        driver.findElement(By.id("Username")).sendKeys("bob");
                        driver.findElement(By.id("Password")).sendKeys("bob");
                        driver.findElement(By.id("login_but")).click();
                        return driver.getCurrentUrl();
                    });
            }

            var options = {
                headers: {
                    "Accept": "application/json",
                    "Authorization": newAuthClientAccessToken
                }
            }

            return loginUser()
                .then(function () {
                    chakram.get(baseAuthUrl + "/identities?client_id=" + authClientFuncTest.id + "&filter=health",
                        options);
                })
                .then(function (getResponse) {
                    expect(getResponse).to.have.status(200);
                    var results = getResponse.body;
                    expect(results).to.be.an("array").that.is.not.empty;
                    var groupFooResult = results[0];
                    expect(groupFooResult.groupName).to.equal(groupFoo.groupName);
                    expect(groupFooResult.entityType).to.equal("Group");
                    expect(groupFooResult.roles).to.be.an("array").that.is.not.empty;
                    expect(groupFooResult.roles.length).to.equal(1);
                    expect(groupFooResult.roles[0]).to.equal(roleFoo.Name);
                });
        });
    });

    describe("associate roles to permissions", function () {
        it("should associate roleFoo with userCanViewPermission and userCanEditPermission", function () {
            authRequestOptions.headers.Authorization = newAuthClientAccessToken;
            var permissions = [];

            return chakram.get(baseAuthUrl + "/permissions/" + userCanViewPermission.Grain + "/" + userCanViewPermission.SecurableItem, authRequestOptions)
                .then(function (getResponse) {
                    expect(getResponse).to.have.status(200);
                    permissions = getResponse.body;
                    return chakram.get(baseAuthUrl + "/roles/" + roleFoo.Grain + "/" + roleFoo.SecurableItem + "/" + encodeURIComponent(roleFoo.Name), authRequestOptions);
                })
                .then(function (getResponse) {
                    expect(getResponse).to.have.status(200);
                    expect(getResponse).to.comprise.of.json([{ name: "FABRIC\\Health Catalyst Viewer" }]);
                    return getResponse.body;
                })
                .then(function (role) {
                    var roleId = role[0].id;
                    return chakram.post(baseAuthUrl + "/roles/" + roleId + "/permissions", permissions, authRequestOptions);
                })
                .then(function (postResponse) {
                    expect(postResponse).to.comprise.of.json({ name: "FABRIC\\Health Catalyst Viewer" });
                    expect(postResponse).to.have.status(201);
                });
        });
    });

    describe("get user permissions", function () {
        it("can get the users permissions", function () {
            //hit the token endpoint for identity with the username and password of the user
            var stringToEncode = "func-test:" + newClientSecret;
            var encodedData = new Buffer(stringToEncode).toString("base64");

            var postData = {
                form: {
                    "grant_type": "password",
                    "username": "bob",
                    "password": "bob"
                },
                headers: {
                    "content-type": "application/x-www-form-urlencoded",
                    "Authorization": "Basic " + encodedData
                }
            };

            return getAccessToken(postData)
                .then(function (accessToken) {
                    expect(accessToken).to.not.be.null;
                    var headers = {
                        headers: {
                            "Accept": "application/json",
                            "Authorization": accessToken
                        }
                    };
                    return chakram.get(baseAuthUrl + "/user/permissions?grain=app&securableItem=func-test", headers);
                })
                .then(function (getResponse) {
                    expect(getResponse).to.have.status(200);

                    var permissions = getResponse.body.permissions;
                    expect(permissions).to.be.an("array").that.is.not.empty;
                    expect(permissions).to.be.an("array").that.includes("app/func-test.userCanEdit");
                    expect(permissions).to.be.an("array").that.includes("app/func-test.userCanView");
                });
        });
    });

    describe("validate security", function () {
        it("should return a 403 when no access token provided", function () {
            return chakram.get(baseAuthUrl + "/clients", requestOptions)
                .then(function (getResponse) {
                    expect(getResponse).to.have.status(403);
                });
        });

        it("should return a 403 when an access token with invalid scope is used", function () {
            return getAccessTokenForAuthClient(newClientSecret)
                .then(function (accessToken) {
                    var options = {
                        headers: {
                            "content-type": "application/json",
                            "Authorization": accessToken
                        }
                    }
                    return chakram.get(baseAuthUrl + "/clients", options);
                })
                .then(function (getResponse) {
                    expect(getResponse).to.have.status(403);
                });
        });
    });
});