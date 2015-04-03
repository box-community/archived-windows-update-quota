# About
This repository provides A Windows command line utility to modify user Box quotas (space allotment) for active accounts. This requires that you [register for a Box Content API key](https://iu.app.box.com/developers/services/edit/) and [generated an access token](https://box-oauth2-mvc.azurewebsites.net/) with a Box account that has enterprise management capabilities.

# A note about large enterprises (30K+ accounts)
This utility can update ~7-8 users per second (~25-29K per hour). Box API access tokens expire after approximately one our, thus if you have an enterprise with more than ~30K users you may wish to specify a `refresh token`, `client id`, and `client secret` with which the utility can fetch a new API access token and continue working uninterrupted.

# Usage
Download and unzip [BoxQuotaUpdate.zip](https://github.com/box-community/update-quota/blob/master/artifacts/BoxQuotaUpdate.zip). From the command line, run

    BoxQuotaUpdate.exe -t ACCESS_TOKEN [-q QUOTA -r REFRESH_TOKEN -i CLIENT_ID -s CLIENT_SECRET]

Where 
+ `ACCESS_TOKEN` is the Box API access token with enterprise management capability. (*required*)
+ `QUOTA` is the new quota to be applied to all active users. Defaults to unlimited. (*optional*)
+ `REFRESH_TOKEN` is the Box API refresh token. (*optional*)
+ `CLIENT_ID` is the Box API application client ID. (*optional*)
+ `CLIENT_SECRET` is the Box API application client secret. (*optional*)
