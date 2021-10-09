pipeline {
    agent any
    stages {
        stage('build') {
            parallel {
                stage('windows') {
                    steps {
                        node('master') {
                            deleteDir()
                            git url: 'https://github.com/ingen084/KyoshinEewViewerIngen.git', branch: env.BRANCH_NAME

                            bat 'publish_custom.bat Windows win10-x64 single false'
                            bat 'publish_custom.bat Windows win10-x64 merged true'

                            archiveArtifacts(artifacts: 'tmp/KyoshinEewViewer_ingen_*.zip', onlyIfSuccessful: true)
                        }
                    }
                }
                stage('linux/mac') {
                    steps {
                        node('lxsv1') {
                            deleteDir()
                            git url: 'https://github.com/ingen084/KyoshinEewViewerIngen.git', branch: env.BRANCH_NAME

                            sh 'chmod +x publish_custom.sh;./publish_custom.sh Linux linux-x64 merged true'
                            sh 'chmod +x publish_osx.sh;./publish_osx.sh MacOS osx-x64 merged true'

                            archiveArtifacts(artifacts: 'tmp/KyoshinEewViewer_ingen_*.zip', onlyIfSuccessful: true)
                        }
                    }
                }
            }
        }
        stage('notify discord') {
            steps {
                withCredentials([string(credentialsId: 'DISCORD_WEBHOOK', variable: 'WebhookUrl')]) {
                    discordSend(description: "build completed!", footer: "Jenkins", link: env.BUILD_URL, result: currentBuild.currentResult, title: "${env.JOB_NAME}#${env.BUILD_NUMBER}", webhookURL: WebhookUrl)
                }
            }
        }
    }
}
