#!/usr/bin/env ruby

require 'html-proofer'

options = {
    :assume_extension => true,     # (true) for extensionless paths
    :http_status_ignore => [ 999 ], # LinkedIn throttling errors
    :typhoeus => {
      # avoid strange SSL errors: https://github.com/gjtorikian/html-proofer/issues/376
      :ssl_verifypeer => false,
      :ssl_verifyhost => 0
    }
}

HTMLProofer.check_directory("./_site", options).run