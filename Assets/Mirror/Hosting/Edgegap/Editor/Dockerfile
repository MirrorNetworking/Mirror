FROM ubuntu:22.04

ARG DEBIAN_FRONTEND=noninteractive

COPY Builds/EdgegapServer /root/build/

WORKDIR /root/

RUN chmod +x /root/build/ServerBuild

RUN apt-get update && \
    apt-get install -y ca-certificates && \
    apt-get clean && \
    update-ca-certificates

ENTRYPOINT [ "/root/build/ServerBuild", "-batchmode", "-nographics"]
